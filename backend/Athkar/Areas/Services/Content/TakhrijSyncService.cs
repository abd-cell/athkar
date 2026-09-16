using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Content;
using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Content.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;
using Athkar.Shareds.Security;

namespace Athkar.Areas.Services.Content;

/// <inheritdoc />
public class TakhrijSyncService : ITakhrijSyncService
{
    private readonly IRepository<Dhikr> adhkar;
    private readonly IRepository<AthkarCategory> categories;
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly ISecurityManager securityManager;
    private readonly ILogger<TakhrijSyncService> logger;

    public TakhrijSyncService(
        IRepository<Dhikr> adhkar,
        IRepository<AthkarCategory> categories,
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        ISecurityManager securityManager,
        ILogger<TakhrijSyncService> logger)
    {
        this.adhkar = adhkar;
        this.categories = categories;
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.securityManager = securityManager;
        this.logger = logger;
    }

    public Task<BaseResponse<TakhrijSyncOutput>> Preview() => Run(apply: false);

    public Task<BaseResponse<TakhrijSyncOutput>> Apply(TakhrijSyncInput? input = null) =>
        Run(apply: true, chosen: input?.DhikrIds is { Count: > 0 } ids ? ids.ToHashSet() : null);

    private async Task<BaseResponse<TakhrijSyncOutput>> Run(bool apply, HashSet<int>? chosen = null)
    {
        var catalog = TakhrijCatalog.Entries;
        if (catalog.Count == 0)
            return BaseResponse<TakhrijSyncOutput>.Fail(ErrorCode.TakhrijCatalogMissing);

        // Matched on the folded Arabic the row already stores. A dhikr an editor
        // retyped with different brackets or diacritics still matches, which is
        // the whole reason that column exists.
        var byFold = catalog
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Fold))
            .GroupBy(entry => entry.Fold)
            .ToDictionary(group => group.Key, group => group.First());

        var rows = await adhkar.Query().ToListAsync();
        var categoryKeys = await categories.Query(includeDeleted: true)
            .ToDictionaryAsync(category => category.Id, category => category.Key);

        var output = new TakhrijSyncOutput { Source = TakhrijCatalog.Source };
        var written = new List<(Dhikr Dhikr, TakhrijSyncRow Report, string Before)>();

        foreach (var dhikr in rows)
        {
            var hasBook = !string.IsNullOrWhiteSpace(dhikr.SourceBook);
            var hasReference = !string.IsNullOrWhiteSpace(dhikr.SourceReference);
            var complete = hasBook && hasReference;

            byFold.TryGetValue(dhikr.SearchText, out var entry);

            // An editor's own attribution is never overwritten — not even by the
            // book's footnote. Where the two differ the row is reported and left
            // as it is: a disagreement between sources is a question for a
            // person, and answering it by overwriting would lose their work
            // silently.
            if (complete)
            {
                if (entry is not null && !string.Equals(entry.Book, dhikr.SourceBook, StringComparison.Ordinal))
                {
                    output.Disagreements++;
                    output.Rows.Add(Describe(dhikr, entry, categoryKeys, TakhrijSyncStatus.Disagreement));
                }
                else
                {
                    output.AlreadyAttributed++;
                }
                continue;
            }

            output.DraftsMissingSource++;

            if (entry is null)
            {
                output.Unmatched++;
                output.Rows.Add(Describe(dhikr, null, categoryKeys, TakhrijSyncStatus.Unmatched));
                continue;
            }

            // Already carries exactly what the footnote says — a row filled by
            // an earlier run. Counted as attributed rather than matched, so a
            // second run reports nothing to do and writes no audit rows saying
            // a field changed from a value to itself.
            if (string.Equals(dhikr.SourceBook, entry.Book, StringComparison.Ordinal) &&
                string.Equals(dhikr.SourceReference, entry.Reference, StringComparison.Ordinal))
            {
                output.DraftsMissingSource--;
                output.AlreadyAttributed++;
                continue;
            }

            output.Matched++;
            var status = string.IsNullOrWhiteSpace(entry.Reference)
                ? TakhrijSyncStatus.BookOnly
                : TakhrijSyncStatus.Filled;

            if (status == TakhrijSyncStatus.Filled) output.Publishable++;
            if (entry.Grade.HasValue) output.Graded++;

            var report = Describe(dhikr, entry, categoryKeys, status);
            report.Chosen = chosen is null || chosen.Contains(dhikr.Id);
            output.Rows.Add(report);

            if (apply && report.Chosen) written.Add((dhikr, report, Describe(dhikr)));
        }

        // What the report counts as filled is what was actually written, so an
        // editor who picked three rows reads "3" rather than the number the
        // check happened to find.
        if (apply) output.Filled = written.Count;

        if (!apply) return new BaseResponse<TakhrijSyncOutput>(output);

        foreach (var (dhikr, report, _) in written)
        {
            dhikr.SourceBook = report.Book;
            dhikr.SourceReference = report.Reference;

            // Only ever added, never replaced: a grading already on the row was
            // somebody's reading, and the footnote's silence is not a reason to
            // drop it.
            if (report.Grade.HasValue && dhikr.Grade is null)
                dhikr.Grade = (HadithGrade)report.Grade.Value;

            if (!string.IsNullOrWhiteSpace(report.GradedBy) && string.IsNullOrWhiteSpace(dhikr.GradedBy))
                dhikr.GradedBy = report.GradedBy;

            // Deliberately absent: dhikr.IsPublished. Filling a field is not
            // reading it, and this project's promise is about what a reader
            // sees, not about what a row contains.
            dhikr.ModifiedBy = securityManager.UserId;
            adhkar.Update(dhikr);
        }

        await unitOfWork.SaveAsync();
        output.Applied = true;

        // No content-version bump. Every row this touches is unpublished, so the
        // catalog a phone downloads is byte for byte what it was — bumping would
        // send every install to re-fetch an identical payload.
        foreach (var (dhikr, report, before) in written)
            await auditService.LogAsync(
                AuditActions.AdhkarTakhrijSync, nameof(Dhikr), dhikr.Id, before, report);

        logger.LogInformation(
            "Takhrij sync: {Filled} of {Matched} chosen rows attributed from {Source}; "
            + "{Unmatched} drafts the footnotes do not account for.",
            written.Count, output.Matched, output.Source, output.Unmatched);

        return new BaseResponse<TakhrijSyncOutput>(output);
    }

    private static TakhrijSyncRow Describe(
        Dhikr dhikr,
        TakhrijCatalog.TakhrijEntry? entry,
        IReadOnlyDictionary<int, string> categoryKeys,
        TakhrijSyncStatus status) =>
        new()
        {
            DhikrId = dhikr.Id,
            Excerpt = Excerpt(dhikr.ArabicText),
            CategoryKey = categoryKeys.TryGetValue(dhikr.CategoryId, out var key) ? key : string.Empty,
            Book = entry?.Book,
            Reference = entry?.Reference,
            Grade = entry?.Grade,
            GradedBy = entry?.GradedBy,
            Note = entry?.Note,
            Status = status,
        };

    /// <summary>The row as it stands, for the audit trail's "before".</summary>
    private static string Describe(Dhikr dhikr) =>
        $"{dhikr.SourceBook} · {dhikr.SourceReference} · {dhikr.Grade} · {dhikr.GradedBy}";

    private static string Excerpt(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= 60 ? trimmed : trimmed[..60] + "…";
    }
}
