using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Configuration;
using Athkar.Areas.Domain.Content;
using Athkar.Areas.Services.Content.Models;
using Athkar.Areas.Services.Localization;
using Athkar.DataAccess.Repositories;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Extensions;
using Athkar.Shareds.Models;
using Athkar.Shareds.Text;

namespace Athkar.Areas.Services.Content;

public class ContentService : IContentService
{
    private readonly IRepository<AthkarCategory> categories;
    private readonly IRepository<Dhikr> adhkar;
    private readonly IRepository<AppConfiguration> configurations;
    private readonly ILanguageResolver languages;

    public ContentService(
        IRepository<AthkarCategory> categories,
        IRepository<Dhikr> adhkar,
        IRepository<AppConfiguration> configurations,
        ILanguageResolver languages)
    {
        this.categories = categories;
        this.adhkar = adhkar;
        this.configurations = configurations;
        this.languages = languages;
    }

    public async Task<BaseResponse<CatalogOutput>> Catalog(string? languageCode, int? knownVersion)
    {
        var language = await languages.Resolve(languageCode);
        var version = await CurrentVersion();

        if (knownVersion is not null && knownVersion == version)
            return new BaseResponse<CatalogOutput>(new CatalogOutput
            {
                Version = version,
                LanguageCode = language,
                IsUpToDate = true,
            });

        // Translations are filtered in the query rather than loaded whole: a
        // corpus with six languages is six times the payload, and the client
        // asked for one.
        var rows = await categories.Query()
            .Where(c => c.IsPublished)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .Select(c => new
            {
                Category = c,
                Translation = c.Translations.FirstOrDefault(t =>
                    !t.IsDeleted && t.LanguageCode == language),
                Adhkar = c.Adhkar
                    .Where(d => !d.IsDeleted && d.IsPublished)
                    .OrderBy(d => d.SortOrder).ThenBy(d => d.Id)
                    .Select(d => new
                    {
                        Dhikr = d,
                        Translation = d.Translations.FirstOrDefault(t =>
                            !t.IsDeleted && t.LanguageCode == language),
                    })
                    .ToList(),
            })
            .ToListAsync();

        var catalog = new CatalogOutput
        {
            Version = version,
            LanguageCode = language,
            Categories =
            [
                .. rows.Select(row => new CategoryOutput
                {
                    Id = row.Category.Id,
                    Key = row.Category.Key,
                    // A chapter with no translation in this language still has a
                    // key, and a key is a worse name than the one an editor would
                    // have written — but it is a great deal better than a blank
                    // row the reader cannot tap.
                    Name = row.Translation?.Name ?? row.Category.Key,
                    Description = row.Translation?.Description,
                    Icon = row.Category.Icon,
                    SortOrder = row.Category.SortOrder,
                    Rhythm = row.Category.Rhythm,
                    Anchor = row.Category.Anchor,
                    Adhkar =
                    [
                        .. row.Adhkar.Select(entry =>
                            Describe(entry.Dhikr, entry.Translation, language)),
                    ],
                }),
            ],
        };

        return new BaseResponse<CatalogOutput>(catalog);
    }

    public async Task<BaseResponse<PageOutput<SearchHitOutput>>> Search(string? languageCode, PageInput input)
    {
        var language = await languages.Resolve(languageCode);
        var term = ArabicText.Normalize(input.Search);

        if (term.Length == 0)
            return new BaseResponse<PageOutput<SearchHitOutput>>(new PageOutput<SearchHitOutput>());

        // Matched against the stored folded column, never against ArabicText:
        // the whole reason SearchText exists is that a reader types «اذكار» and
        // the corpus says «أَذْكَار».
        var query = adhkar.Query()
            .Where(d => d.IsPublished && d.SearchText.Contains(term))
            .Where(d => d.Category!.IsPublished);

        var total = await query.CountAsync();

        var rows = await query
            .OrderBy(d => d.CategoryId).ThenBy(d => d.SortOrder)
            .Paginate(input)
            .Select(d => new
            {
                Dhikr = d,
                Translation = d.Translations.FirstOrDefault(t =>
                    !t.IsDeleted && t.LanguageCode == language),
                CategoryKey = d.Category!.Key,
                CategoryName = d.Category!.Translations
                    .Where(t => !t.IsDeleted && t.LanguageCode == language)
                    .Select(t => t.Name)
                    .FirstOrDefault(),
            })
            .ToListAsync();

        return new BaseResponse<PageOutput<SearchHitOutput>>(new PageOutput<SearchHitOutput>
        {
            TotalRows = total,
            Data =
            [
                .. rows.Select(row =>
                {
                    var hit = Describe(row.Dhikr, row.Translation, language);
                    return new SearchHitOutput
                    {
                        Id = hit.Id,
                        CategoryId = hit.CategoryId,
                        SortOrder = hit.SortOrder,
                        ArabicText = hit.ArabicText,
                        RepeatCount = hit.RepeatCount,
                        Translation = hit.Translation,
                        Transliteration = hit.Transliteration,
                        Virtue = hit.Virtue,
                        SourceBook = hit.SourceBook,
                        SourceReference = hit.SourceReference,
                        Grade = hit.Grade,
                        GradedBy = hit.GradedBy,
                        CategoryKey = row.CategoryKey,
                        CategoryName = row.CategoryName ?? row.CategoryKey,
                    };
                }),
            ],
        });
    }

    /// <summary>
    /// Shapes one dhikr for a reader.
    ///
    /// The <see cref="ContentRules.SourceLanguage"/> check is what stops the
    /// Arabic payload carrying the Arabic twice: a reader on the Arabic build
    /// has the text, and "translation" in their own language is a field with
    /// nothing to say.
    /// </summary>
    private static DhikrOutput Describe(Dhikr dhikr, DhikrTranslation? translation, string language)
    {
        var isSourceLanguage = language == ContentRules.SourceLanguage;

        return new DhikrOutput
        {
            Id = dhikr.Id,
            CategoryId = dhikr.CategoryId,
            SortOrder = dhikr.SortOrder,
            ArabicText = dhikr.ArabicText,
            RepeatCount = dhikr.RepeatCount,
            Translation = isSourceLanguage ? null : translation?.Translation,
            Transliteration = isSourceLanguage ? null : translation?.Transliteration,
            Virtue = translation?.Virtue,
            SourceBook = dhikr.SourceBook,
            SourceReference = dhikr.SourceReference,
            Grade = dhikr.Grade,
            GradedBy = dhikr.GradedBy,
        };
    }

    private async Task<int> CurrentVersion()
    {
        var configuration = await configurations.Query().OrderBy(c => c.Id).FirstOrDefaultAsync();
        return configuration?.ContentVersion ?? 1;
    }
}
