using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Athkar.Areas.Domain.Content;
using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Configuration;
using Athkar.Areas.Services.Quran.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;
using Athkar.Shareds.Models.Config;
using Athkar.Shareds.Security;
using Athkar.Shareds.Text;
using Catalog = Athkar.Areas.Services.Quran.QuranicAthkarCatalog;

namespace Athkar.Areas.Services.Quran;

/// <summary>
/// Re-reads the Qur'anic adhkar from the canonical source and reconciles the
/// stored rows with it.
///
/// This is the one place in the system where narrated text is rewritten without
/// a person typing the replacement, so it is deliberately narrow:
///
/// - It touches <b>only</b> rows in the <c>quran</c> chapter whose
///   <c>SourceBook</c> is «القرآن الكريم» and whose reference it recognises. A row an
///   editor has moved or re-attributed is reported and left alone.
/// - It never deletes. A dhikr that has left the catalogue stays where it is;
///   removing content is an editor's decision and this has no opinion about it.
/// - It is all or nothing. A source that fails halfway writes nothing, because a
///   half-synced chapter is worse than an un-synced one — it looks finished.
/// - <see cref="Preview"/> is the same computation without the write, so an admin
///   sees what would change before agreeing to it.
/// </summary>
[ScopedInjectable]
public interface IQuranAthkarSyncService
{
    /// <summary>What a sync would change. Reads the source; writes nothing.</summary>
    Task<BaseResponse<QuranSyncOutput>> Preview();

    /// <summary>Applies the differences, audits them, and bumps the content version if anything moved.</summary>
    Task<BaseResponse<QuranSyncOutput>> Apply();
}

public class QuranAthkarSyncService : IQuranAthkarSyncService
{
    private readonly IQuranMcpClient source;
    private readonly IRepository<AthkarCategory> categories;
    private readonly IRepository<Dhikr> adhkar;
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IAppConfigurationService configuration;
    private readonly ISecurityManager securityManager;
    private readonly ILogger<QuranAthkarSyncService> logger;
    private readonly QuranMcpSettings settings;

    public QuranAthkarSyncService(
        IQuranMcpClient source,
        IRepository<AthkarCategory> categories,
        IRepository<Dhikr> adhkar,
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IAppConfigurationService configuration,
        ISecurityManager securityManager,
        ILogger<QuranAthkarSyncService> logger,
        IOptions<QuranMcpSettings> options)
    {
        this.source = source;
        this.categories = categories;
        this.adhkar = adhkar;
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.configuration = configuration;
        this.securityManager = securityManager;
        this.logger = logger;
        settings = options.Value;
    }

    public Task<BaseResponse<QuranSyncOutput>> Preview() => Run(apply: false);

    public Task<BaseResponse<QuranSyncOutput>> Apply() => Run(apply: true);

    private async Task<BaseResponse<QuranSyncOutput>> Run(bool apply)
    {
        if (!source.IsEnabled)
            return BaseResponse<QuranSyncOutput>.Fail(ErrorCode.QuranSourceDisabled);

        var blocks = Catalog.Blocks;
        if (blocks.Count == 0)
            return BaseResponse<QuranSyncOutput>.Fail(
                ErrorCode.QuranSourceUnreachable,
                "The Qur'anic adhkar catalogue is missing. Re-run tools/quran-mcp/pull.py.");

        var category = await categories.FirstOrDefaultAsync(c => c.Key == Catalog.CategoryKey);
        if (category is null)
            return BaseResponse<QuranSyncOutput>.Fail(ErrorCode.CategoryNotFound);

        IReadOnlyList<QuranBlockText> canonical;
        try
        {
            canonical = await source.FetchBlocks(blocks.Select(b => b.Reference).ToList());
        }
        catch (QuranMcpException ex)
        {
            // A third party being down is not this server erroring — the envelope
            // says so, and ExceptionMiddleware never sees it.
            logger.LogWarning(ex, "Qur'anic adhkar sync could not read the canonical source.");
            return BaseResponse<QuranSyncOutput>.Fail(ErrorCode.QuranSourceUnreachable, ex.Message);
        }

        var byReference = canonical.ToDictionary(b => b.Reference);

        var rows = await adhkar.Query()
            .Include(d => d.Translations)
            .Where(d => d.CategoryId == category.Id && d.SourceBook == Catalog.SourceBook)
            .ToListAsync();

        var byLocator = rows
            .Where(d => d.SourceReference is not null)
            .GroupBy(d => d.SourceReference!)
            .ToDictionary(g => g.Key, g => g.First());

        var output = new QuranSyncOutput
        {
            Source = settings.BaseUrl,
            Checked = blocks.Count,
        };

        var touched = new List<(Dhikr Row, QuranSyncChange Change)>();

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (!byReference.TryGetValue(block.Reference, out var fresh)) continue;

            var locator = Catalog.Locator(block);
            // Fitted to the columns before anything is compared, so a block that
            // is too long for the schema is not reported as "changed" on every
            // single run because the stored row can never equal the full text.
            var arabic = TextFit.Fit(string.Join(' ', fresh.Display), ContentRules.MaxTextLength);
            var search = TextFit.Fit(
                ArabicText.Normalize(string.Join(' ', fresh.Search)), ContentRules.MaxIndexedSearchLength);
            var english = TextFit.Fit(
                QuranText.StripMarkup(string.Join(' ', fresh.English)), ContentRules.MaxTextLength);

            var change = new QuranSyncChange
            {
                Key = block.Key,
                Reference = locator,
                Name = block.NameArabic,
            };

            if (!byLocator.TryGetValue(locator, out var row))
            {
                change.Action = QuranSyncAction.Added;
                change.CanonicalArabic = arabic;
                output.Added++;
                output.Changes.Add(change);

                if (apply) touched.Add((NewRow(category.Id, i, locator, arabic, search, english), change));
                continue;
            }

            change.DhikrId = row.Id;

            // A published row with no grade or the wrong book has been taken over
            // by an editor. Nothing here is authoritative enough to overrule that.
            if (row.Grade != HadithGrade.QuranVerse)
            {
                change.Action = QuranSyncAction.Skipped;
                output.Skipped++;
                output.Changes.Add(change);
                continue;
            }

            var storedEnglish = row.Translations.FirstOrDefault(t => t.LanguageCode == "en");

            if (row.ArabicText != arabic) change.Fields.Add("arabic");
            if (row.SearchText != search) change.Fields.Add("search");
            if ((storedEnglish?.Translation ?? string.Empty) != english) change.Fields.Add("en");

            if (change.Fields.Count == 0)
            {
                change.Action = QuranSyncAction.Unchanged;
                continue;
            }

            change.Action = QuranSyncAction.Changed;
            if (change.Fields.Contains("arabic"))
            {
                change.StoredArabic = row.ArabicText;
                change.CanonicalArabic = arabic;
            }

            output.Changed++;
            output.Changes.Add(change);

            if (!apply) continue;

            row.ArabicText = arabic;
            row.SearchText = search;
            row.ModifiedBy = securityManager.UserId;

            var ar = row.Translations.FirstOrDefault(t => t.LanguageCode == "ar");
            if (ar is not null) ar.Translation = arabic;

            if (storedEnglish is not null) storedEnglish.Translation = english;
            else row.Translations.Add(new DhikrTranslation { LanguageCode = "en", Translation = english });

            adhkar.Update(row);
            touched.Add((row, change));
        }

        if (!apply || touched.Count == 0)
            return new BaseResponse<QuranSyncOutput>(output);

        foreach (var (row, change) in touched)
            if (change.Action == QuranSyncAction.Added)
                await adhkar.AddAsync(row);

        await unitOfWork.SaveAsync();

        output.Applied = true;
        output.ContentVersion = await configuration.BumpContentVersion();

        // Audited per row, not once for the batch: a reviewer asking "when did
        // this verse change and to what" needs the answer filed against the
        // dhikr, the way every other content edit is.
        foreach (var (row, change) in touched)
        {
            change.DhikrId = row.Id;
            await auditService.LogAsync(AuditActions.QuranAthkarSync, nameof(Dhikr), row.Id, null, change);
        }

        logger.LogInformation(
            "Qur'anic adhkar sync: {Added} added, {Changed} changed, {Skipped} skipped; content version {Version}.",
            output.Added, output.Changed, output.Skipped, output.ContentVersion);

        return new BaseResponse<QuranSyncOutput>(output);
    }

    private Dhikr NewRow(int categoryId, int sortOrder, string locator, string arabic, string search, string english)
    {
        var row = new Dhikr
        {
            CategoryId = categoryId,
            SortOrder = sortOrder,
            ArabicText = arabic,
            SearchText = search,
            RepeatCount = 1,
            SourceBook = Catalog.SourceBook,
            SourceReference = locator,
            Grade = HadithGrade.QuranVerse,

            // Published on creation, and only because the gate is already
            // satisfied: a Qur'anic row arrives with its book and its locator
            // attached. Nothing here can produce an unsourced row, which is the
            // condition ErrorCode.SourceRequired exists to catch.
            IsPublished = true,
            CreatedBy = securityManager.UserId,
        };

        row.Translations.Add(new DhikrTranslation { LanguageCode = "ar", Translation = arabic });
        row.Translations.Add(new DhikrTranslation { LanguageCode = "en", Translation = english });

        return row;
    }
}
