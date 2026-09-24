using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Recitations;
using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Configuration;
using Athkar.Areas.Services.Content.Models;
using Athkar.Areas.Services.Recitations.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Extensions;
using Athkar.Shareds.Models;
using Athkar.Shareds.Security;

namespace Athkar.Areas.Services.Recitations;

/// <summary>
/// Curating the reciters the sync brought in.
///
/// Rules peculiar to this slice: a reciter cannot be published with no
/// published recording (he would be a page with nothing to play, and the
/// catalogue would drop him anyway — better to say so at the switch than to
/// have an editor wonder why he never appeared), and a portrait must be https
/// for the same reason a stream must.
/// </summary>
public class RecitationAdminService : IRecitationAdminService
{
    private readonly IRepository<Reciter> reciters;
    private readonly IRepository<ReciterTranslation> reciterTranslations;
    private readonly IRepository<Recitation> recitations;
    private readonly IRepository<RecitationTranslation> recitationTranslations;
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IAppConfigurationService configuration;
    private readonly ISecurityManager securityManager;

    public RecitationAdminService(
        IRepository<Reciter> reciters,
        IRepository<ReciterTranslation> reciterTranslations,
        IRepository<Recitation> recitations,
        IRepository<RecitationTranslation> recitationTranslations,
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IAppConfigurationService configuration,
        ISecurityManager securityManager)
    {
        this.reciters = reciters;
        this.reciterTranslations = reciterTranslations;
        this.recitations = recitations;
        this.recitationTranslations = recitationTranslations;
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.configuration = configuration;
        this.securityManager = securityManager;
    }

    public async Task<BaseResponse<PageOutput<AdminReciterOutput>>> ListReciters(ReciterListInput input)
    {
        var query = Loaded();

        if (input.IsPublished is { } published)
            query = query.Where(r => r.IsPublished == published);

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var term = input.Search.Trim();
            query = query.Where(r =>
                r.Key.Contains(term) ||
                r.Translations.Any(t => !t.IsDeleted && t.Name.Contains(term)));
        }

        var total = await query.CountAsync();

        var rows = await query
            // Published and featured first: the list an editor opens on is the
            // one readers see, and two hundred drafts come after it.
            .OrderByDescending(r => r.IsPublished)
            .ThenByDescending(r => r.IsFeatured)
            .ThenBy(r => r.SortOrder).ThenBy(r => r.Id)
            .Paginate(input)
            .ToListAsync();

        return new BaseResponse<PageOutput<AdminReciterOutput>>(new PageOutput<AdminReciterOutput>
        {
            TotalRows = total,
            Data = [.. rows.Select(Describe)],
        });
    }

    public async Task<BaseResponse<AdminReciterOutput>> GetReciter(int id)
    {
        var reciter = await Loaded().FirstOrDefaultAsync(r => r.Id == id);

        return reciter is null
            ? BaseResponse<AdminReciterOutput>.Fail(ErrorCode.ReciterNotFound)
            : new BaseResponse<AdminReciterOutput>(Describe(reciter));
    }

    public async Task<BaseResponse<AdminReciterOutput>> UpdateReciter(int id, ReciterInput input)
    {
        var reciter = await Loaded().FirstOrDefaultAsync(r => r.Id == id);
        if (reciter is null) return BaseResponse<AdminReciterOutput>.Fail(ErrorCode.ReciterNotFound);

        if (!HasSourceLanguage(input.Translations))
            return BaseResponse<AdminReciterOutput>.Fail(ErrorCode.ValidationError,
                $"A '{ContentRules.SourceLanguage}' name is required.");

        if (Blank(input.ImageUrl) is { } image && !IsSecureUrl(image))
            return BaseResponse<AdminReciterOutput>.Fail(ErrorCode.InsecureStreamUrl);

        if (input.IsPublished && !reciter.Recitations.Any(x => !x.IsDeleted && x.IsPublished))
            return BaseResponse<AdminReciterOutput>.Fail(ErrorCode.ValidationError,
                "Publish at least one of this reciter's recordings first.");

        var before = Describe(reciter);

        reciter.ImageUrl = Blank(input.ImageUrl);
        reciter.IsFeatured = input.IsFeatured;
        reciter.SortOrder = input.SortOrder;
        reciter.IsPublished = input.IsPublished;
        reciter.ModifiedBy = securityManager.UserId;
        reciters.Update(reciter);

        SyncTranslations(
            reciter.Translations, input.Translations,
            (row, name) => { row.Name = name; reciterTranslations.Update(row); },
            row => reciterTranslations.SoftDelete(row),
            (language, name) => reciter.Translations.Add(new ReciterTranslation
            {
                ReciterId = reciter.Id, LanguageCode = language, Name = name,
            }));

        await unitOfWork.SaveAsync();
        await configuration.BumpContentVersion();

        var output = Describe(reciter);
        await auditService.LogAsync(AuditActions.ReciterUpdate, nameof(Reciter), reciter.Id, before, output);

        return new BaseResponse<AdminReciterOutput>(output);
    }

    public async Task<BaseResponse<AdminReciterOutput>> UpdateRecitation(
        int reciterId, int recitationId, RecitationInput input)
    {
        var reciter = await Loaded().FirstOrDefaultAsync(r => r.Id == reciterId);
        if (reciter is null) return BaseResponse<AdminReciterOutput>.Fail(ErrorCode.ReciterNotFound);

        var recording = reciter.Recitations.FirstOrDefault(x => x.Id == recitationId && !x.IsDeleted);
        if (recording is null) return BaseResponse<AdminReciterOutput>.Fail(ErrorCode.RecitationNotFound);

        if (!HasSourceLanguage(input.Translations))
            return BaseResponse<AdminReciterOutput>.Fail(ErrorCode.ValidationError,
                $"A '{ContentRules.SourceLanguage}' name is required.");

        var before = Describe(reciter);

        recording.IsPublished = input.IsPublished;
        recording.SortOrder = input.SortOrder;
        recording.ModifiedBy = securityManager.UserId;
        recitations.Update(recording);

        SyncTranslations(
            recording.Translations, input.Translations,
            (row, name) => { row.Name = name; recitationTranslations.Update(row); },
            row => recitationTranslations.SoftDelete(row),
            (language, name) => recording.Translations.Add(new RecitationTranslation
            {
                RecitationId = recording.Id, LanguageCode = language, Name = name,
            }));

        // Withdrawing a published reciter's last published recording withdraws
        // him too — the catalogue could not show him, and a "published" switch
        // that lies is worse than one that turned itself off.
        if (reciter.IsPublished && !reciter.Recitations.Any(x => !x.IsDeleted && x.IsPublished))
        {
            reciter.IsPublished = false;
            reciters.Update(reciter);
        }

        await unitOfWork.SaveAsync();
        await configuration.BumpContentVersion();

        var output = Describe(reciter);
        await auditService.LogAsync(AuditActions.RecitationUpdate, nameof(Recitation), recording.Id, before, output);

        return new BaseResponse<AdminReciterOutput>(output);
    }

    public async Task<BaseResponse> DeleteReciter(int id)
    {
        var reciter = await Loaded().FirstOrDefaultAsync(r => r.Id == id);
        if (reciter is null) return BaseResponse.Fail(ErrorCode.ReciterNotFound);

        var before = Describe(reciter);

        foreach (var recording in reciter.Recitations.Where(x => !x.IsDeleted))
        {
            recitationTranslations.SoftDeleteRange(recording.Translations.Where(t => !t.IsDeleted));
            recitations.SoftDelete(recording);
        }
        reciterTranslations.SoftDeleteRange(reciter.Translations.Where(t => !t.IsDeleted));
        reciters.SoftDelete(reciter);

        await unitOfWork.SaveAsync();
        await configuration.BumpContentVersion();

        await auditService.LogAsync(AuditActions.ReciterDelete, nameof(Reciter), id, before, null);
        return new BaseResponse();
    }

    public async Task<BaseResponse> ReorderReciters(ReorderInput input)
    {
        var ids = input.Items.Select(i => i.Id).ToList();
        var rows = await reciters.Where(r => ids.Contains(r.Id)).ToListAsync();

        foreach (var row in rows)
        {
            row.SortOrder = input.Items.First(i => i.Id == row.Id).SortOrder;
            reciters.Update(row);
        }

        await unitOfWork.SaveAsync();
        await configuration.BumpContentVersion();

        await auditService.LogAsync(AuditActions.ReciterReorder, nameof(Reciter), null, null, input.Items);
        return new BaseResponse();
    }

    private IQueryable<Reciter> Loaded() =>
        reciters.Query()
            .Include(r => r.Translations)
            .Include(r => r.Recitations).ThenInclude(x => x.Translations);

    /// <summary>
    /// Replaces a translation set with the incoming one: update what matches,
    /// soft-delete what is absent, add what is new. One entry per language,
    /// last one wins — the content service's own rule.
    /// </summary>
    private static void SyncTranslations<T>(
        ICollection<T> current,
        IEnumerable<TranslationInput> wanted,
        Action<T, string> update,
        Action<T> remove,
        Action<string, string> add) where T : Shareds.Models.Base.TranslationEntity
    {
        var incoming = wanted
            .Where(t => !string.IsNullOrWhiteSpace(t.LanguageCode) && !string.IsNullOrWhiteSpace(t.Title))
            .GroupBy(t => t.LanguageCode.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.Last().Title.Trim());

        foreach (var existing in current.Where(t => !t.IsDeleted).ToList())
        {
            if (incoming.Remove(existing.LanguageCode, out var name)) update(existing, name);
            else remove(existing);
        }

        foreach (var (language, name) in incoming) add(language, name);
    }

    private static bool HasSourceLanguage(IEnumerable<TranslationInput> translations) =>
        translations.Any(t =>
            string.Equals(t.LanguageCode?.Trim(), ContentRules.SourceLanguage, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(t.Title));

    private static bool IsSecureUrl(string raw) =>
        Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static string? Blank(string? raw)
    {
        var text = raw?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    internal static AdminReciterOutput Describe(Reciter reciter)
    {
        var live = reciter.Translations.Where(t => !t.IsDeleted).ToList();

        return new AdminReciterOutput
        {
            Id = reciter.Id,
            Key = reciter.Key,
            ExternalId = reciter.ExternalId,
            ImageUrl = reciter.ImageUrl,
            IsFeatured = reciter.IsFeatured,
            SortOrder = reciter.SortOrder,
            IsPublished = reciter.IsPublished,
            Name = live.FirstOrDefault(t => t.LanguageCode == ContentRules.SourceLanguage)?.Name ?? reciter.Key,
            TranslatedLanguages = [.. live.Select(t => t.LanguageCode).Order()],
            Translations = [.. live.Select(t => new TranslationInput { LanguageCode = t.LanguageCode, Title = t.Name })],
            Recitations =
            [
                .. reciter.Recitations
                    .Where(x => !x.IsDeleted)
                    .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
                    .Select(x =>
                    {
                        var names = x.Translations.Where(t => !t.IsDeleted).ToList();
                        return new AdminRecitationOutput
                        {
                            Id = x.Id,
                            ExternalId = x.ExternalId,
                            Name = names.FirstOrDefault(t => t.LanguageCode == ContentRules.SourceLanguage)?.Name
                                   ?? $"#{x.Id}",
                            ServerUrl = x.ServerUrl,
                            SurahCount = RecitationSurahs.Parse(x.SurahList).Count,
                            HasTiming = x.TimingReadId is not null,
                            SourceName = x.SourceName,
                            SourceUrl = x.SourceUrl,
                            SortOrder = x.SortOrder,
                            IsPublished = x.IsPublished,
                            Translations =
                            [
                                .. names.Select(t => new TranslationInput { LanguageCode = t.LanguageCode, Title = t.Name }),
                            ],
                        };
                    }),
            ],
        };
    }
}

/// <summary>The one place a stored surah list is read, so the catalogue and the console agree on it.</summary>
public static class RecitationSurahs
{
    public static List<int> Parse(string? list) =>
    [
        .. (list ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var n) ? n : 0)
            .Where(n => n is >= 1 and <= 114)
            .Distinct()
            .Order(),
    ];
}
