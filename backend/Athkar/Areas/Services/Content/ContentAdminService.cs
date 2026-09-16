using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Content;
using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Configuration;
using Athkar.Areas.Services.Content.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Extensions;
using Athkar.Shareds.Models;
using Athkar.Shareds.Security;
using Athkar.Shareds.Text;

namespace Athkar.Areas.Services.Content;

/// <summary>
/// Content editing, as the CMS drives it.
///
/// Two rules run through every method here and are worth stating once:
///
/// 1. <b>Publishing requires a source.</b> A row with no book and no reference
///    may be saved as a draft and may not be published. The project's one
///    distinguishing promise is that every dhikr on screen can be traced, and
///    the cheapest place to keep that promise is the only door content comes
///    through.
///
/// 2. <b>Any change a reader would see bumps the content version.</b> That
///    single integer is the whole sync protocol, so forgetting it is not a
///    cosmetic bug: it is an edit that never arrives.
/// </summary>
public class ContentAdminService : IContentAdminService
{
    private readonly IRepository<AthkarCategory> categories;
    private readonly IRepository<CategoryTranslation> categoryTranslations;
    private readonly IRepository<Dhikr> adhkar;
    private readonly IRepository<DhikrTranslation> dhikrTranslations;
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IAppConfigurationService configuration;
    private readonly ISecurityManager securityManager;

    public ContentAdminService(
        IRepository<AthkarCategory> categories,
        IRepository<CategoryTranslation> categoryTranslations,
        IRepository<Dhikr> adhkar,
        IRepository<DhikrTranslation> dhikrTranslations,
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IAppConfigurationService configuration,
        ISecurityManager securityManager)
    {
        this.categories = categories;
        this.categoryTranslations = categoryTranslations;
        this.adhkar = adhkar;
        this.dhikrTranslations = dhikrTranslations;
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.configuration = configuration;
        this.securityManager = securityManager;
    }

    // ────────────────────────────── categories ──────────────────────────────

    public async Task<BaseResponse<PageOutput<AdminCategoryOutput>>> ListCategories(PageInput input)
    {
        var query = categories.Query().Include(c => c.Translations).AsQueryable();

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var term = input.Search.Trim();
            query = query.Where(c =>
                c.Key.Contains(term) ||
                c.Translations.Any(t => !t.IsDeleted && t.Name.Contains(term)));
        }

        var total = await query.CountAsync();

        var rows = await query
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .Paginate(input)
            .Select(c => new
            {
                Category = c,
                DhikrCount = c.Adhkar.Count(d => !d.IsDeleted),
                PublishedCount = c.Adhkar.Count(d => !d.IsDeleted && d.IsPublished),
            })
            .ToListAsync();

        return new BaseResponse<PageOutput<AdminCategoryOutput>>(new PageOutput<AdminCategoryOutput>
        {
            TotalRows = total,
            Data =
            [
                .. rows.Select(row =>
                {
                    var output = Describe(row.Category);
                    output.DhikrCount = row.DhikrCount;
                    output.PublishedDhikrCount = row.PublishedCount;
                    return output;
                }),
            ],
        });
    }

    public async Task<BaseResponse<AdminCategoryOutput>> GetCategory(int id)
    {
        var category = await categories.Query()
            .Include(c => c.Translations)
            .FirstOrDefaultAsync(c => c.Id == id);

        return category is null
            ? BaseResponse<AdminCategoryOutput>.Fail(ErrorCode.CategoryNotFound)
            : new BaseResponse<AdminCategoryOutput>(Describe(category));
    }

    public async Task<BaseResponse<AdminCategoryOutput>> CreateCategory(CategoryInput input)
    {
        var key = input.Key.Trim().ToLowerInvariant();

        if (await categories.AnyAsync(c => c.Key == key))
            return BaseResponse<AdminCategoryOutput>.Fail(ErrorCode.DuplicateKey);

        if (!HasSourceLanguage(input.Translations))
            return BaseResponse<AdminCategoryOutput>.Fail(ErrorCode.ValidationError,
                $"A '{ContentRules.SourceLanguage}' translation is required.");

        var category = new AthkarCategory
        {
            Key = key,
            Icon = Blank(input.Icon),
            SortOrder = input.SortOrder,
            Rhythm = input.Rhythm,
            Anchor = input.Anchor,
            Section = input.Section,
            IsPublished = input.IsPublished,
            CreatedBy = securityManager.UserId,
        };

        foreach (var translation in Distinct(input.Translations))
            category.Translations.Add(new CategoryTranslation
            {
                LanguageCode = translation.LanguageCode.Trim().ToLowerInvariant(),
                Name = translation.Title.Trim(),
                Description = Blank(translation.Body),
            });

        await categories.AddAsync(category);
        await unitOfWork.SaveAsync();
        await configuration.BumpContentVersion();

        var output = Describe(category);
        await auditService.LogAsync(AuditActions.CategoryCreate, nameof(AthkarCategory),
            category.Id, null, output);

        return new BaseResponse<AdminCategoryOutput>(output);
    }

    public async Task<BaseResponse<AdminCategoryOutput>> UpdateCategory(int id, CategoryInput input)
    {
        var category = await categories.Query()
            .Include(c => c.Translations)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category is null) return BaseResponse<AdminCategoryOutput>.Fail(ErrorCode.CategoryNotFound);

        var key = input.Key.Trim().ToLowerInvariant();
        if (key != category.Key && await categories.AnyAsync(c => c.Key == key))
            return BaseResponse<AdminCategoryOutput>.Fail(ErrorCode.DuplicateKey);

        if (!HasSourceLanguage(input.Translations))
            return BaseResponse<AdminCategoryOutput>.Fail(ErrorCode.ValidationError,
                $"A '{ContentRules.SourceLanguage}' translation is required.");

        var before = Describe(category);

        category.Key = key;
        category.Icon = Blank(input.Icon);
        category.SortOrder = input.SortOrder;
        category.Rhythm = input.Rhythm;
        category.Anchor = input.Anchor;
        category.Section = input.Section;
        category.IsPublished = input.IsPublished;
        category.ModifiedBy = securityManager.UserId;
        categories.Update(category);

        SyncCategoryTranslations(category, input.Translations);

        await unitOfWork.SaveAsync();
        await configuration.BumpContentVersion();

        var output = Describe(category);
        await auditService.LogAsync(AuditActions.CategoryUpdate, nameof(AthkarCategory),
            category.Id, before, output);

        return new BaseResponse<AdminCategoryOutput>(output);
    }

    public async Task<BaseResponse> DeleteCategory(int id)
    {
        var category = await categories.Query()
            .Include(c => c.Translations)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category is null) return BaseResponse.Fail(ErrorCode.CategoryNotFound);

        // Refused rather than cascaded. Deleting a chapter is a click; deleting
        // forty sourced adhkar as a side effect of that click is not something
        // an editor should be able to do without noticing.
        if (await adhkar.AnyAsync(d => d.CategoryId == id))
            return BaseResponse.Fail(ErrorCode.CategoryNotEmpty);

        var before = Describe(category);

        categoryTranslations.SoftDeleteRange(category.Translations.Where(t => !t.IsDeleted));
        categories.SoftDelete(category);

        await unitOfWork.SaveAsync();
        await configuration.BumpContentVersion();

        await auditService.LogAsync(AuditActions.CategoryDelete, nameof(AthkarCategory),
            id, before, null);

        return new BaseResponse();
    }

    public async Task<BaseResponse> ReorderCategories(ReorderInput input)
    {
        var ids = input.Items.Select(i => i.Id).ToList();
        var rows = await categories.Where(c => ids.Contains(c.Id)).ToListAsync();

        foreach (var row in rows)
        {
            row.SortOrder = input.Items.First(i => i.Id == row.Id).SortOrder;
            categories.Update(row);
        }

        await unitOfWork.SaveAsync();
        await configuration.BumpContentVersion();

        await auditService.LogAsync(AuditActions.CategoryReorder, nameof(AthkarCategory),
            null, null, input.Items);

        return new BaseResponse();
    }

    // ──────────────────────────────── adhkar ────────────────────────────────

    public async Task<BaseResponse<PageOutput<AdminDhikrOutput>>> ListAdhkar(int? categoryId, PageInput input)
    {
        var query = adhkar.Query()
            .Include(d => d.Translations)
            .Include(d => d.Category)
            .AsQueryable();

        if (categoryId is not null)
            query = query.Where(d => d.CategoryId == categoryId);

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var term = ArabicText.Normalize(input.Search);
            query = query.Where(d => d.SearchText.Contains(term));
        }

        var total = await query.CountAsync();
        var rows = await query
            .OrderBy(d => d.CategoryId).ThenBy(d => d.SortOrder).ThenBy(d => d.Id)
            .Paginate(input)
            .ToListAsync();

        return new BaseResponse<PageOutput<AdminDhikrOutput>>(new PageOutput<AdminDhikrOutput>
        {
            TotalRows = total,
            Data = [.. rows.Select(Describe)],
        });
    }

    public async Task<BaseResponse<AdminDhikrOutput>> GetDhikr(int id)
    {
        var dhikr = await adhkar.Query()
            .Include(d => d.Translations)
            .Include(d => d.Category)
            .FirstOrDefaultAsync(d => d.Id == id);

        return dhikr is null
            ? BaseResponse<AdminDhikrOutput>.Fail(ErrorCode.DhikrNotFound)
            : new BaseResponse<AdminDhikrOutput>(Describe(dhikr));
    }

    public async Task<BaseResponse<AdminDhikrOutput>> CreateDhikr(DhikrInput input)
    {
        if (!await categories.AnyAsync(c => c.Id == input.CategoryId))
            return BaseResponse<AdminDhikrOutput>.Fail(ErrorCode.CategoryNotFound);

        var arabic = input.ArabicText.Trim();
        if (arabic.Length == 0)
            return BaseResponse<AdminDhikrOutput>.Fail(ErrorCode.ArabicTextRequired);

        if (input.IsPublished && !HasSource(input.SourceBook, input.SourceReference))
            return BaseResponse<AdminDhikrOutput>.Fail(ErrorCode.SourceRequired);

        var dhikr = new Dhikr
        {
            CategoryId = input.CategoryId,
            SortOrder = input.SortOrder,
            ArabicText = arabic,
            SearchText = ArabicText.Normalize(arabic),
            RepeatCount = Math.Clamp(input.RepeatCount, 1, ContentRules.MaxRepeatCount),
            SourceBook = Blank(input.SourceBook),
            SourceReference = Blank(input.SourceReference),
            Grade = input.Grade,
            GradedBy = Blank(input.GradedBy),
            IsPublished = input.IsPublished,
            CreatedBy = securityManager.UserId,
        };

        foreach (var translation in Distinct(input.Translations))
            dhikr.Translations.Add(BuildTranslation(translation));

        await adhkar.AddAsync(dhikr);
        await unitOfWork.SaveAsync();
        await configuration.BumpContentVersion();

        var output = Describe(dhikr);
        await auditService.LogAsync(AuditActions.DhikrCreate, nameof(Dhikr), dhikr.Id, null, output);

        return new BaseResponse<AdminDhikrOutput>(output);
    }

    public async Task<BaseResponse<AdminDhikrOutput>> UpdateDhikr(int id, DhikrInput input)
    {
        var dhikr = await adhkar.Query()
            .Include(d => d.Translations)
            .Include(d => d.Category)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (dhikr is null) return BaseResponse<AdminDhikrOutput>.Fail(ErrorCode.DhikrNotFound);

        if (input.CategoryId != dhikr.CategoryId &&
            !await categories.AnyAsync(c => c.Id == input.CategoryId))
            return BaseResponse<AdminDhikrOutput>.Fail(ErrorCode.CategoryNotFound);

        var arabic = input.ArabicText.Trim();
        if (arabic.Length == 0)
            return BaseResponse<AdminDhikrOutput>.Fail(ErrorCode.ArabicTextRequired);

        if (input.IsPublished && !HasSource(input.SourceBook, input.SourceReference))
            return BaseResponse<AdminDhikrOutput>.Fail(ErrorCode.SourceRequired);

        var before = Describe(dhikr);

        dhikr.CategoryId = input.CategoryId;
        dhikr.SortOrder = input.SortOrder;
        dhikr.ArabicText = arabic;
        // Rewritten on every save rather than only when the text changed: the
        // folding rules themselves evolve, and a row saved under an older set
        // would otherwise keep an index entry nobody can match.
        dhikr.SearchText = ArabicText.Normalize(arabic);
        dhikr.RepeatCount = Math.Clamp(input.RepeatCount, 1, ContentRules.MaxRepeatCount);
        dhikr.SourceBook = Blank(input.SourceBook);
        dhikr.SourceReference = Blank(input.SourceReference);
        dhikr.Grade = input.Grade;
        dhikr.GradedBy = Blank(input.GradedBy);
        dhikr.IsPublished = input.IsPublished;
        dhikr.ModifiedBy = securityManager.UserId;
        adhkar.Update(dhikr);

        SyncDhikrTranslations(dhikr, input.Translations);

        await unitOfWork.SaveAsync();
        await configuration.BumpContentVersion();

        var output = Describe(dhikr);
        await auditService.LogAsync(AuditActions.DhikrUpdate, nameof(Dhikr), dhikr.Id, before, output);

        return new BaseResponse<AdminDhikrOutput>(output);
    }

    public async Task<BaseResponse> DeleteDhikr(int id)
    {
        var dhikr = await adhkar.Query()
            .Include(d => d.Translations)
            .Include(d => d.Category)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (dhikr is null) return BaseResponse.Fail(ErrorCode.DhikrNotFound);

        var before = Describe(dhikr);

        dhikrTranslations.SoftDeleteRange(dhikr.Translations.Where(t => !t.IsDeleted));
        adhkar.SoftDelete(dhikr);

        await unitOfWork.SaveAsync();
        await configuration.BumpContentVersion();

        await auditService.LogAsync(AuditActions.DhikrDelete, nameof(Dhikr), id, before, null);

        return new BaseResponse();
    }

    public async Task<BaseResponse> ReorderAdhkar(ReorderInput input)
    {
        var ids = input.Items.Select(i => i.Id).ToList();
        var rows = await adhkar.Where(d => ids.Contains(d.Id)).ToListAsync();

        foreach (var row in rows)
        {
            row.SortOrder = input.Items.First(i => i.Id == row.Id).SortOrder;
            adhkar.Update(row);
        }

        await unitOfWork.SaveAsync();
        await configuration.BumpContentVersion();

        return new BaseResponse();
    }

    public async Task<BaseResponse<AdminDhikrOutput>> SetDhikrPublished(int id, bool isPublished)
    {
        var dhikr = await adhkar.Query()
            .Include(d => d.Translations)
            .Include(d => d.Category)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (dhikr is null) return BaseResponse<AdminDhikrOutput>.Fail(ErrorCode.DhikrNotFound);

        if (isPublished && !HasSource(dhikr.SourceBook, dhikr.SourceReference))
            return BaseResponse<AdminDhikrOutput>.Fail(ErrorCode.SourceRequired);

        dhikr.IsPublished = isPublished;
        dhikr.ModifiedBy = securityManager.UserId;
        adhkar.Update(dhikr);

        await unitOfWork.SaveAsync();
        await configuration.BumpContentVersion();

        var output = Describe(dhikr);
        await auditService.LogAsync(
            isPublished ? AuditActions.DhikrPublish : AuditActions.DhikrUnpublish,
            nameof(Dhikr), dhikr.Id, null, output);

        return new BaseResponse<AdminDhikrOutput>(output);
    }

    // ─────────────────────────────── helpers ───────────────────────────────

    /// <summary>
    /// A row is publishable once it names where it came from. A grade without a
    /// book is not enough — the reader has to be able to go and look.
    /// </summary>
    private static bool HasSource(string? book, string? reference) =>
        !string.IsNullOrWhiteSpace(book) && !string.IsNullOrWhiteSpace(reference);

    private static bool HasSourceLanguage(IEnumerable<TranslationInput> translations) =>
        translations.Any(t =>
            string.Equals(t.LanguageCode?.Trim(), ContentRules.SourceLanguage,
                StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(t.Title));

    /// <summary>
    /// One entry per language, last one wins. The CMS should not send duplicates
    /// and a careless import will, and a duplicate here becomes a unique-index
    /// violation halfway through a save.
    /// </summary>
    private static IEnumerable<TranslationInput> Distinct(IEnumerable<TranslationInput> translations) =>
        translations
            .Where(t => !string.IsNullOrWhiteSpace(t.LanguageCode))
            .GroupBy(t => t.LanguageCode.Trim().ToLowerInvariant())
            .Select(g => g.Last());

    private void SyncCategoryTranslations(AthkarCategory category, List<TranslationInput> wanted)
    {
        var incoming = Distinct(wanted).ToDictionary(t => t.LanguageCode.Trim().ToLowerInvariant());

        foreach (var existing in category.Translations.Where(t => !t.IsDeleted).ToList())
        {
            if (incoming.TryGetValue(existing.LanguageCode, out var update))
            {
                existing.Name = update.Title.Trim();
                existing.Description = Blank(update.Body);
                categoryTranslations.Update(existing);
                incoming.Remove(existing.LanguageCode);
            }
            else
            {
                categoryTranslations.SoftDelete(existing);
            }
        }

        foreach (var (code, translation) in incoming)
            category.Translations.Add(new CategoryTranslation
            {
                CategoryId = category.Id,
                LanguageCode = code,
                Name = translation.Title.Trim(),
                Description = Blank(translation.Body),
            });
    }

    private void SyncDhikrTranslations(Dhikr dhikr, List<TranslationInput> wanted)
    {
        var incoming = Distinct(wanted).ToDictionary(t => t.LanguageCode.Trim().ToLowerInvariant());

        foreach (var existing in dhikr.Translations.Where(t => !t.IsDeleted).ToList())
        {
            if (incoming.TryGetValue(existing.LanguageCode, out var update))
            {
                existing.Translation = update.Title.Trim();
                existing.Transliteration = Blank(update.Secondary);
                existing.Virtue = Blank(update.Virtue);
                dhikrTranslations.Update(existing);
                incoming.Remove(existing.LanguageCode);
            }
            else
            {
                dhikrTranslations.SoftDelete(existing);
            }
        }

        foreach (var (code, translation) in incoming)
        {
            var row = BuildTranslation(translation);
            row.DhikrId = dhikr.Id;
            dhikr.Translations.Add(row);
        }
    }

    private static DhikrTranslation BuildTranslation(TranslationInput input) => new()
    {
        LanguageCode = input.LanguageCode.Trim().ToLowerInvariant(),
        Translation = input.Title.Trim(),
        Transliteration = Blank(input.Secondary),
        Virtue = Blank(input.Virtue),
    };

    private static AdminCategoryOutput Describe(AthkarCategory category)
    {
        var live = category.Translations.Where(t => !t.IsDeleted).ToList();

        return new AdminCategoryOutput
        {
            Id = category.Id,
            Key = category.Key,
            Icon = category.Icon,
            SortOrder = category.SortOrder,
            Rhythm = category.Rhythm,
            Anchor = category.Anchor,
            Section = category.Section,
            IsPublished = category.IsPublished,
            TranslatedLanguages = [.. live.Select(t => t.LanguageCode).Order()],
            Translations =
            [
                .. live.Select(t => new TranslationInput
                {
                    LanguageCode = t.LanguageCode,
                    Title = t.Name,
                    Body = t.Description,
                }),
            ],
        };
    }

    private static AdminDhikrOutput Describe(Dhikr dhikr)
    {
        var live = dhikr.Translations.Where(t => !t.IsDeleted).ToList();

        return new AdminDhikrOutput
        {
            Id = dhikr.Id,
            CategoryId = dhikr.CategoryId,
            CategoryKey = dhikr.Category?.Key ?? string.Empty,
            SortOrder = dhikr.SortOrder,
            ArabicText = dhikr.ArabicText,
            RepeatCount = dhikr.RepeatCount,
            SourceBook = dhikr.SourceBook,
            SourceReference = dhikr.SourceReference,
            Grade = dhikr.Grade,
            GradedBy = dhikr.GradedBy,
            IsPublished = dhikr.IsPublished,
            IsPublishable = HasSource(dhikr.SourceBook, dhikr.SourceReference),
            TranslatedLanguages = [.. live.Select(t => t.LanguageCode).Order()],
            Translations =
            [
                .. live.Select(t => new TranslationInput
                {
                    LanguageCode = t.LanguageCode,
                    Title = t.Translation,
                    Secondary = t.Transliteration,
                    Virtue = t.Virtue,
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
