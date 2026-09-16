using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Radio;
using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Configuration;
using Athkar.Areas.Services.Content.Models;
using Athkar.Areas.Services.Radio.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Extensions;
using Athkar.Shareds.Models;
using Athkar.Shareds.Security;

namespace Athkar.Areas.Services.Radio;

/// <summary>
/// Editing the station list.
///
/// Two of the rules here are the content slice's own: a station must carry a
/// name in the source language, and anything a reader would notice bumps
/// <c>AppConfiguration.ContentVersion</c>. The third is peculiar to this slice —
/// the stream must be https, checked here rather than left to the phone,
/// because a cleartext stream is refused by the device's network policy, where
/// nobody in the console would ever see it happen.
/// </summary>
public class RadioAdminService : IRadioAdminService
{
    private readonly IRepository<RadioStation> stations;
    private readonly IRepository<RadioStationTranslation> translations;
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IAppConfigurationService configuration;
    private readonly ISecurityManager securityManager;

    public RadioAdminService(
        IRepository<RadioStation> stations,
        IRepository<RadioStationTranslation> translations,
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IAppConfigurationService configuration,
        ISecurityManager securityManager)
    {
        this.stations = stations;
        this.translations = translations;
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.configuration = configuration;
        this.securityManager = securityManager;
    }

    public async Task<BaseResponse<PageOutput<AdminRadioStationOutput>>> ListStations(PageInput input)
    {
        var query = stations.Query().Include(s => s.Translations).AsQueryable();

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var term = input.Search.Trim();
            query = query.Where(s =>
                s.Key.Contains(term) ||
                s.Translations.Any(t => !t.IsDeleted && t.Name.Contains(term)));
        }

        var total = await query.CountAsync();

        var rows = await query
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .Paginate(input)
            .ToListAsync();

        return new BaseResponse<PageOutput<AdminRadioStationOutput>>(new PageOutput<AdminRadioStationOutput>
        {
            TotalRows = total,
            Data = [.. rows.Select(Describe)],
        });
    }

    public async Task<BaseResponse<AdminRadioStationOutput>> GetStation(int id)
    {
        var station = await stations.Query()
            .Include(s => s.Translations)
            .FirstOrDefaultAsync(s => s.Id == id);

        return station is null
            ? BaseResponse<AdminRadioStationOutput>.Fail(ErrorCode.RadioStationNotFound)
            : new BaseResponse<AdminRadioStationOutput>(Describe(station));
    }

    public async Task<BaseResponse<AdminRadioStationOutput>> CreateStation(RadioStationInput input)
    {
        var key = input.Key.Trim().ToLowerInvariant();

        if (await stations.AnyAsync(s => s.Key == key))
            return BaseResponse<AdminRadioStationOutput>.Fail(ErrorCode.DuplicateKey);

        if (Validate(input) is { } failure)
            return BaseResponse<AdminRadioStationOutput>.Fail(failure.Code, failure.Message);

        var station = new RadioStation
        {
            Key = key,
            StreamUrl = input.StreamUrl.Trim(),
            LogoUrl = Blank(input.LogoUrl),
            SortOrder = input.SortOrder,
            IsPublished = input.IsPublished,
            CreatedBy = securityManager.UserId,
        };

        foreach (var translation in Distinct(input.Translations))
            station.Translations.Add(Build(translation));

        await stations.AddAsync(station);
        await unitOfWork.SaveAsync();
        await configuration.BumpContentVersion();

        var output = Describe(station);
        await auditService.LogAsync(AuditActions.RadioStationCreate, nameof(RadioStation),
            station.Id, null, output);

        return new BaseResponse<AdminRadioStationOutput>(output);
    }

    public async Task<BaseResponse<AdminRadioStationOutput>> UpdateStation(int id, RadioStationInput input)
    {
        var station = await stations.Query()
            .Include(s => s.Translations)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (station is null)
            return BaseResponse<AdminRadioStationOutput>.Fail(ErrorCode.RadioStationNotFound);

        var key = input.Key.Trim().ToLowerInvariant();
        if (key != station.Key && await stations.AnyAsync(s => s.Key == key))
            return BaseResponse<AdminRadioStationOutput>.Fail(ErrorCode.DuplicateKey);

        if (Validate(input) is { } failure)
            return BaseResponse<AdminRadioStationOutput>.Fail(failure.Code, failure.Message);

        var before = Describe(station);

        station.Key = key;
        station.StreamUrl = input.StreamUrl.Trim();
        station.LogoUrl = Blank(input.LogoUrl);
        station.SortOrder = input.SortOrder;
        station.IsPublished = input.IsPublished;
        station.ModifiedBy = securityManager.UserId;
        stations.Update(station);

        SyncTranslations(station, input.Translations);

        await unitOfWork.SaveAsync();
        await configuration.BumpContentVersion();

        var output = Describe(station);
        await auditService.LogAsync(AuditActions.RadioStationUpdate, nameof(RadioStation),
            station.Id, before, output);

        return new BaseResponse<AdminRadioStationOutput>(output);
    }

    public async Task<BaseResponse> DeleteStation(int id)
    {
        var station = await stations.Query()
            .Include(s => s.Translations)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (station is null) return BaseResponse.Fail(ErrorCode.RadioStationNotFound);

        var before = Describe(station);

        translations.SoftDeleteRange(station.Translations.Where(t => !t.IsDeleted));
        stations.SoftDelete(station);

        await unitOfWork.SaveAsync();
        await configuration.BumpContentVersion();

        await auditService.LogAsync(AuditActions.RadioStationDelete, nameof(RadioStation),
            id, before, null);

        return new BaseResponse();
    }

    public async Task<BaseResponse> ReorderStations(ReorderInput input)
    {
        var ids = input.Items.Select(i => i.Id).ToList();
        var rows = await stations.Where(s => ids.Contains(s.Id)).ToListAsync();

        foreach (var row in rows)
        {
            row.SortOrder = input.Items.First(i => i.Id == row.Id).SortOrder;
            stations.Update(row);
        }

        await unitOfWork.SaveAsync();
        await configuration.BumpContentVersion();

        await auditService.LogAsync(AuditActions.RadioStationReorder, nameof(RadioStation),
            null, null, input.Items);

        return new BaseResponse();
    }

    /// <summary>
    /// What a station must be before it is saved, draft or not. The https rule
    /// applies to a draft as much as to a published row: a draft that cannot
    /// play is a draft nobody can check.
    /// </summary>
    private static (ErrorCode Code, string? Message)? Validate(RadioStationInput input)
    {
        if (!HasSourceLanguage(input.Translations))
            return (ErrorCode.ValidationError,
                $"A '{ContentRules.SourceLanguage}' translation is required.");

        if (!IsSecureUrl(input.StreamUrl)) return (ErrorCode.InsecureStreamUrl, null);

        if (Blank(input.LogoUrl) is { } logo && !IsSecureUrl(logo))
            return (ErrorCode.InsecureStreamUrl, null);

        return null;
    }

    private static bool IsSecureUrl(string? raw) =>
        Uri.TryCreate(raw?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static bool HasSourceLanguage(IEnumerable<TranslationInput> translations) =>
        translations.Any(t =>
            string.Equals(t.LanguageCode?.Trim(), ContentRules.SourceLanguage,
                StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(t.Title));

    /// <summary>One entry per language, last one wins. See the content service's own note.</summary>
    private static IEnumerable<TranslationInput> Distinct(IEnumerable<TranslationInput> translations) =>
        translations
            .Where(t => !string.IsNullOrWhiteSpace(t.LanguageCode))
            .GroupBy(t => t.LanguageCode.Trim().ToLowerInvariant())
            .Select(g => g.Last());

    private void SyncTranslations(RadioStation station, List<TranslationInput> wanted)
    {
        var incoming = Distinct(wanted).ToDictionary(t => t.LanguageCode.Trim().ToLowerInvariant());

        foreach (var existing in station.Translations.Where(t => !t.IsDeleted).ToList())
        {
            if (incoming.TryGetValue(existing.LanguageCode, out var update))
            {
                existing.Name = update.Title.Trim();
                existing.Provider = Blank(update.Body);
                translations.Update(existing);
                incoming.Remove(existing.LanguageCode);
            }
            else
            {
                translations.SoftDelete(existing);
            }
        }

        foreach (var (_, translation) in incoming)
        {
            var row = Build(translation);
            row.StationId = station.Id;
            station.Translations.Add(row);
        }
    }

    private static RadioStationTranslation Build(TranslationInput input) => new()
    {
        LanguageCode = input.LanguageCode.Trim().ToLowerInvariant(),
        Name = input.Title.Trim(),
        Provider = Blank(input.Body),
    };

    private static AdminRadioStationOutput Describe(RadioStation station)
    {
        var live = station.Translations.Where(t => !t.IsDeleted).ToList();

        return new AdminRadioStationOutput
        {
            Id = station.Id,
            Key = station.Key,
            StreamUrl = station.StreamUrl,
            LogoUrl = station.LogoUrl,
            SortOrder = station.SortOrder,
            IsPublished = station.IsPublished,
            TranslatedLanguages = [.. live.Select(t => t.LanguageCode).Order()],
            Translations =
            [
                .. live.Select(t => new TranslationInput
                {
                    LanguageCode = t.LanguageCode,
                    Title = t.Name,
                    Body = t.Provider,
                }),
            ],
        };
    }

    private static string? Blank(string? raw)
    {
        var text = raw?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
