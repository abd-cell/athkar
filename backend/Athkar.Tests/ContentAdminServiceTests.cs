using Athkar.Areas.Domain.Content;
using Athkar.Areas.Services.Content;
using Athkar.Areas.Services.Content.Models;
using Athkar.Shareds.Models;
using Athkar.Shareds.Text;
using Athkar.Tests.TestDoubles;

namespace Athkar.Tests;

/// <summary>
/// The two rules the whole content slice rests on:
///
/// 1. Nothing publishes without a source.
/// 2. Every change a reader would see bumps the content version.
///
/// The first is the project's one distinguishing promise; the second is the
/// entire sync protocol, so forgetting it is an edit that never arrives.
/// </summary>
public class ContentAdminServiceTests
{
    private readonly InMemoryRepository<AthkarCategory> categories = new();
    private readonly InMemoryRepository<CategoryTranslation> categoryTranslations = new();
    private readonly InMemoryRepository<Dhikr> adhkar = new();
    private readonly InMemoryRepository<DhikrTranslation> dhikrTranslations = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly FakeAuditService audit = new();
    private readonly FakeAppConfigurationService configuration = new();

    private ContentAdminService Service() => new(
        categories, categoryTranslations, adhkar, dhikrTranslations,
        unitOfWork, audit, configuration, new FakeSecurityManager());

    private static DhikrInput SourcedDhikr(int categoryId, bool published = true) => new()
    {
        CategoryId = categoryId,
        ArabicText = "سُبْحَانَ اللهِ وَبِحَمْدِهِ",
        RepeatCount = 100,
        SourceBook = "صحيح مسلم",
        SourceReference = "٢٦٩١",
        Grade = Shareds.Enums.HadithGrade.Sahih,
        IsPublished = published,
        Translations = [],
    };

    private AthkarCategory SeedCategory()
    {
        var category = new AthkarCategory { Key = "morning", IsPublished = true };
        categories.Seed(category);
        return category;
    }

    // ── the source rule ──

    [Fact]
    public async Task A_dhikr_without_a_source_cannot_be_published()
    {
        var category = SeedCategory();

        var input = SourcedDhikr(category.Id);
        input.SourceBook = null;

        var response = await Service().CreateDhikr(input);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.SourceRequired, response.ErrorCode);
        Assert.Empty(adhkar.All);
    }

    [Fact]
    public async Task A_reference_alone_is_not_a_source()
    {
        var category = SeedCategory();

        var input = SourcedDhikr(category.Id);
        input.SourceBook = "   ";

        var response = await Service().CreateDhikr(input);

        Assert.Equal(ErrorCode.SourceRequired, response.ErrorCode);
    }

    [Fact]
    public async Task An_unsourced_draft_is_allowed_to_exist()
    {
        // Drafting is how content gets written. The rule is about publishing.
        var category = SeedCategory();

        var input = SourcedDhikr(category.Id, published: false);
        input.SourceBook = null;
        input.SourceReference = null;

        var response = await Service().CreateDhikr(input);

        Assert.True(response.Success);
        Assert.False(response.Data!.IsPublishable);
    }

    [Fact]
    public async Task Publishing_an_unsourced_draft_is_refused()
    {
        var category = SeedCategory();
        adhkar.Seed(new Dhikr { CategoryId = category.Id, ArabicText = "نص", IsPublished = false });

        var response = await Service().SetDhikrPublished(adhkar.All[0].Id, true);

        Assert.Equal(ErrorCode.SourceRequired, response.ErrorCode);
        Assert.False(adhkar.All[0].IsPublished);
    }

    [Fact]
    public async Task Unpublishing_is_always_allowed()
    {
        // A row already on a phone must be withdrawable whatever state it is in
        // — that is the reviewer's stop button.
        var category = SeedCategory();
        adhkar.Seed(new Dhikr { CategoryId = category.Id, ArabicText = "نص", IsPublished = true });

        var response = await Service().SetDhikrPublished(adhkar.All[0].Id, false);

        Assert.True(response.Success);
        Assert.False(adhkar.All[0].IsPublished);
    }

    // ── the version rule ──

    [Fact]
    public async Task Every_change_a_reader_would_see_bumps_the_content_version()
    {
        var category = SeedCategory();
        var service = Service();

        var created = await service.CreateDhikr(SourcedDhikr(category.Id));
        Assert.Equal(1, configuration.BumpCount);

        await service.UpdateDhikr(created.Data!.Id, SourcedDhikr(category.Id));
        Assert.Equal(2, configuration.BumpCount);

        await service.SetDhikrPublished(created.Data!.Id, false);
        Assert.Equal(3, configuration.BumpCount);

        await service.DeleteDhikr(created.Data!.Id);
        Assert.Equal(4, configuration.BumpCount);
    }

    [Fact]
    public async Task A_refused_change_does_not_bump_the_version()
    {
        var category = SeedCategory();

        var input = SourcedDhikr(category.Id);
        input.SourceBook = null;

        await Service().CreateDhikr(input);

        Assert.Equal(0, configuration.BumpCount);
    }

    // ── the search column ──

    [Fact]
    public async Task Saving_a_dhikr_writes_its_folded_search_text()
    {
        var category = SeedCategory();

        await Service().CreateDhikr(SourcedDhikr(category.Id));

        Assert.Equal(
            ArabicText.Normalize("سُبْحَانَ اللهِ وَبِحَمْدِهِ"),
            adhkar.All[0].SearchText);
        Assert.Equal("سبحان الله وبحمده", adhkar.All[0].SearchText);
    }

    [Fact]
    public async Task Editing_the_text_rewrites_the_search_column()
    {
        var category = SeedCategory();
        var service = Service();

        var created = await service.CreateDhikr(SourcedDhikr(category.Id));

        var edited = SourcedDhikr(category.Id);
        edited.ArabicText = "أَسْتَغْفِرُ اللهَ";
        await service.UpdateDhikr(created.Data!.Id, edited);

        Assert.Equal("استغفر الله", adhkar.All[0].SearchText);
    }

    // ── categories ──

    [Fact]
    public async Task A_category_holding_adhkar_is_not_deleted_by_accident()
    {
        var category = SeedCategory();
        adhkar.Seed(new Dhikr { CategoryId = category.Id, ArabicText = "نص" });

        var response = await Service().DeleteCategory(category.Id);

        Assert.Equal(ErrorCode.CategoryNotEmpty, response.ErrorCode);
        Assert.False(categories.All[0].IsDeleted);
    }

    [Fact]
    public async Task A_category_needs_a_name_in_the_source_language()
    {
        var response = await Service().CreateCategory(new CategoryInput
        {
            Key = "evening",
            Translations = [new TranslationInput { LanguageCode = "en", Title = "Evening" }],
        });

        Assert.Equal(ErrorCode.ValidationError, response.ErrorCode);
    }

    [Fact]
    public async Task Category_keys_are_unique()
    {
        SeedCategory();

        var response = await Service().CreateCategory(new CategoryInput
        {
            Key = "morning",
            Translations = [new TranslationInput { LanguageCode = "ar", Title = "أذكار الصباح" }],
        });

        Assert.Equal(ErrorCode.DuplicateKey, response.ErrorCode);
    }

    [Fact]
    public async Task Replacing_translations_removes_the_ones_left_out()
    {
        // A language absent from the payload means "removed" — that is what
        // makes the round trip lossless rather than additive.
        var service = Service();

        var created = await service.CreateCategory(new CategoryInput
        {
            Key = "sleep",
            Translations =
            [
                new TranslationInput { LanguageCode = "ar", Title = "أذكار النوم" },
                new TranslationInput { LanguageCode = "en", Title = "Before sleep" },
            ],
        });

        var updated = await service.UpdateCategory(created.Data!.Id, new CategoryInput
        {
            Key = "sleep",
            Translations = [new TranslationInput { LanguageCode = "ar", Title = "أذكار النوم" }],
        });

        Assert.Equal(["ar"], updated.Data!.TranslatedLanguages);
    }

    // ── the trail ──

    [Fact]
    public async Task Content_changes_are_audited()
    {
        var category = SeedCategory();
        var service = Service();

        var created = await service.CreateDhikr(SourcedDhikr(category.Id));
        await service.SetDhikrPublished(created.Data!.Id, false);

        Assert.Contains("content.dhikr.create", audit.Actions);
        Assert.Contains("content.dhikr.unpublish", audit.Actions);
    }
}
