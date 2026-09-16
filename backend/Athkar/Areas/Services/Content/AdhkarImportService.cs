using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Content;
using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Configuration;
using Athkar.Areas.Services.Content.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;
using Athkar.Shareds.Security;
using Athkar.Shareds.Text;

namespace Athkar.Areas.Services.Content;

/// <summary>
/// Imports the أبواب and adhkar of حصن المسلم into the content tables.
///
/// The rule that shapes every line of this is the one absence in the source: it
/// carries no book, no hadith number and no grading. So:
///
/// - **Every dhikr arrives unpublished.** Not as a concession but as the point:
///   <see cref="ErrorCode.SourceRequired"/> exists to keep unattributed text off
///   a reader's screen, and an import that published 267 rows around it would be
///   the largest hole anyone could put in this project's one promise. An editor
///   adds the takhrij and publishes, one row at a time, as they always have.
/// - **The أبواب arrive unpublished too**, and for a different reason: a chapter
///   whose every dhikr is a draft is an empty list on the reader's screen. 129
///   of those is not a fuller app, it is a broken one. So the chapter is
///   published by the editor, in the ordinary way, once it has something in it.
/// - **It only ever adds.** Nothing is edited and nothing is deleted. A chapter
///   that already holds some of these same adhkar is an editor's curation of
///   exactly this set, and is left alone rather than interleaved with the
///   book's ordering — reported as skipped.
/// - **Adhkar are matched on folded Arabic**, so running it twice adds nothing
///   the second time, and a dhikr an editor has already typed by hand is
///   recognised as the same dhikr despite different brackets or diacritics.
/// - **<see cref="Preview"/> is the same computation without the write**, because
///   creating 129 chapters is not something to discover after the click.
/// </summary>
[ScopedInjectable]
public interface IAdhkarImportService
{
    /// <summary>What an import would add. Writes nothing.</summary>
    Task<BaseResponse<AdhkarImportOutput>> Preview();

    /// <summary>Adds the missing أبواب and adhkar, audits them, and bumps the content version.</summary>
    Task<BaseResponse<AdhkarImportOutput>> Apply();
}

public class AdhkarImportService : IAdhkarImportService
{
    private readonly IRepository<AthkarCategory> categories;
    private readonly IRepository<Dhikr> adhkar;
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IAppConfigurationService configuration;
    private readonly ISecurityManager securityManager;
    private readonly ILogger<AdhkarImportService> logger;

    public AdhkarImportService(
        IRepository<AthkarCategory> categories,
        IRepository<Dhikr> adhkar,
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IAppConfigurationService configuration,
        ISecurityManager securityManager,
        ILogger<AdhkarImportService> logger)
    {
        this.categories = categories;
        this.adhkar = adhkar;
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.configuration = configuration;
        this.securityManager = securityManager;
        this.logger = logger;
    }

    public Task<BaseResponse<AdhkarImportOutput>> Preview() => Run(apply: false);

    public Task<BaseResponse<AdhkarImportOutput>> Apply() => Run(apply: true);

    private async Task<BaseResponse<AdhkarImportOutput>> Run(bool apply)
    {
        var corpus = AdhkarCatalog.Chapters;
        if (corpus.Count == 0)
            return BaseResponse<AdhkarImportOutput>.Fail(ErrorCode.AdhkarCorpusMissing);

        // Deleted rows are read back deliberately. An editor who removed a
        // chapter made a decision, and an import that quietly restored it would
        // be a bug that takes a week to notice.
        var existingCategories = await categories.Query(includeDeleted: true)
            .ToDictionaryAsync(c => c.Key, c => c);

        var existingText = await adhkar.Query(includeDeleted: true)
            .Select(d => new { d.CategoryId, d.SearchText })
            .ToListAsync();

        var foldedByCategory = existingText
            .GroupBy(d => d.CategoryId)
            .ToDictionary(g => g.Key, g => g.Select(d => d.SearchText).ToHashSet());

        var output = new AdhkarImportOutput
        {
            Source = AdhkarCatalog.Source,
            ChaptersChecked = corpus.Count,
        };

        // The highest slot in use, so imported chapters queue after whatever an
        // editor has arranged rather than shuffling into the middle of it.
        var nextSort = existingCategories.Count == 0
            ? 0
            : existingCategories.Values.Max(c => c.SortOrder) + 1;

        var created = new List<(AthkarCategory Category, AdhkarImportChapter Report)>();
        var added = new List<Dhikr>();

        foreach (var chapter in corpus)
        {
            var report = new AdhkarImportChapter
            {
                Key = chapter.Key,
                Title = chapter.TitleArabic,
                SourceId = chapter.SourceId,
            };

            existingCategories.TryGetValue(chapter.Key, out var category);
            report.CategoryId = category?.Id;

            var folded = category is not null && foldedByCategory.TryGetValue(category.Id, out var set)
                ? set
                : [];

            // Fold once per dhikr and de-duplicate within the chapter too: the
            // source repeats a couple of adhkar across its own sub-headings.
            var incoming = new List<(AdhkarCatalog.ImportedDhikr Item, string Folded)>();
            var seen = new HashSet<string>();

            foreach (var item in chapter.Adhkar)
            {
                var key = ArabicText.Normalize(item.Text);
                if (key.Length == 0 || !seen.Add(key)) continue;

                if (folded.Contains(key)) report.Present++;
                else incoming.Add((item, key));
            }

            report.Adding = incoming.Count;

            if (category is null)
            {
                report.Action = AdhkarImportAction.Added;
                output.ChaptersAdded++;
            }
            else if (report.Present > 0 && incoming.Count > 0)
            {
                // Some of this باب's adhkar are already here, which means an
                // editor has curated this exact set. Adding the rest would
                // interleave the book's ordering with theirs, and this has no
                // opinion about which should win. A chapter holding *different*
                // adhkar is not that case and is extended.
                report.Action = AdhkarImportAction.Skipped;
                report.Adding = 0;
                output.ChaptersSkipped++;
                output.AdhkarAlreadyPresent += report.Present;
                output.Chapters.Add(report);
                continue;
            }
            else if (incoming.Count > 0)
            {
                report.Action = AdhkarImportAction.Extended;
            }
            else
            {
                report.Action = AdhkarImportAction.Unchanged;
                output.AdhkarAlreadyPresent += report.Present;
                continue;
            }

            output.AdhkarAdded += incoming.Count;
            output.AdhkarAlreadyPresent += report.Present;
            output.Chapters.Add(report);

            if (!apply) continue;

            if (category is null)
            {
                category = NewCategory(chapter, nextSort++);
                created.Add((category, report));
                await categories.AddAsync(category);
            }

            foreach (var (item, key) in incoming)
                added.Add(NewDhikr(category, item, key, added.Count));
        }

        output.DraftsAwaitingSource = output.AdhkarAdded;

        if (!apply || (output.ChaptersAdded == 0 && output.AdhkarAdded == 0))
            return new BaseResponse<AdhkarImportOutput>(output);

        // Categories first: a dhikr needs its chapter's id, and the created rows
        // have none until they are saved.
        await unitOfWork.SaveAsync();

        foreach (var (category, report) in created)
        {
            report.CategoryId = category.Id;
            foreach (var dhikr in added.Where(d => ReferenceEquals(d.Category, category)))
                dhikr.CategoryId = category.Id;
        }

        foreach (var dhikr in added)
        {
            dhikr.Category = null;
            await adhkar.AddAsync(dhikr);
        }

        await unitOfWork.SaveAsync();

        output.Applied = true;
        output.ContentVersion = await configuration.BumpContentVersion();

        foreach (var report in output.Chapters)
            await auditService.LogAsync(
                AuditActions.AdhkarImport, nameof(AthkarCategory), report.CategoryId, null, report);

        logger.LogInformation(
            "Adhkar import: {Chapters} chapters and {Adhkar} adhkar added as drafts from {Source}; content version {Version}.",
            output.ChaptersAdded, output.AdhkarAdded, output.Source, output.ContentVersion);

        return new BaseResponse<AdhkarImportOutput>(output);
    }

    private AthkarCategory NewCategory(AdhkarCatalog.ImportedChapter chapter, int sortOrder)
    {
        var category = new AthkarCategory
        {
            Key = chapter.Key,
            Icon = "book",
            SortOrder = sortOrder,
            Rhythm = CategoryRhythm.None,

            // No anchor. Which prayer a chapter belongs to decides when a
            // reminder fires, and guessing it from a title would schedule a
            // notification nobody asked for.
            Anchor = PrayerAnchor.None,

            // Unpublished, like its contents, and for a plainer reason than the
            // takhrij rule: every dhikr in it is a draft, so publishing the
            // chapter would put an empty list in front of a reader — a hundred
            // and more of them. The editor publishes it once it holds something.
            IsPublished = false,
            CreatedBy = securityManager.UserId,
        };

        category.Translations.Add(new CategoryTranslation
        {
            LanguageCode = "ar",
            Name = chapter.TitleArabic,
        });

        // No English. The source is Arabic only, and a machine-rendered chapter
        // name would read as though somebody had approved it. The translations
        // editor is where that gets filled in.
        return category;
    }

    private Dhikr NewDhikr(AthkarCategory category, AdhkarCatalog.ImportedDhikr item, string folded, int order)
    {
        var text = TextFit.Fit(item.Text, ContentRules.MaxTextLength);

        var dhikr = new Dhikr
        {
            CategoryId = category.Id,

            // Held so the id can be copied over once the category is saved. EF
            // would do this through the navigation, but these rows are added to
            // the repository rather than to the category's collection.
            Category = category.Id == 0 ? category : null,

            SortOrder = order,
            ArabicText = text,
            SearchText = TextFit.Fit(folded, ContentRules.MaxIndexedSearchLength),
            RepeatCount = Math.Clamp(item.Repeat, 1, ContentRules.MaxRepeatCount),

            // The three fields this source does not have, left honestly empty.
            SourceBook = null,
            SourceReference = null,
            Grade = null,
            GradedBy = null,

            // And therefore not publishable. This is the whole shape of the
            // import: it fills the editors' queue, never the reader's screen.
            IsPublished = false,
            CreatedBy = securityManager.UserId,
        };

        dhikr.Translations.Add(new DhikrTranslation { LanguageCode = "ar", Translation = text });

        return dhikr;
    }
}
