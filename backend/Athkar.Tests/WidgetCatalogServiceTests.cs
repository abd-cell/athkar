using Athkar.Areas.Domain.Content;
using Athkar.Areas.Domain.Widgets;
using Athkar.Areas.Services.Content.Models;
using Athkar.Areas.Services.Widgets;
using Athkar.Areas.Services.Widgets.Models;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;
using Athkar.Tests.TestDoubles;

namespace Athkar.Tests;

/// <summary>
/// The widget gallery.
///
/// Three rules here are worth a test because each one fails *silently* on a
/// surface nobody is looking at:
///
/// <list type="bullet">
/// <item>A key is the contract with the app's renderer registry. Two rows
/// holding one key makes which widget a reader gets depend on row order, and an
/// entry renamed after release stops matching anything the app can draw.</item>
/// <item>A hidden entry must actually leave the gallery, and the change must
/// move the version — otherwise no phone has any reason to re-fetch.</item>
/// <item>An entry with no wording the reader can read must be dropped, not
/// shown under its key. A key is a developer's handle.</item>
/// </list>
/// </summary>
public class WidgetCatalogServiceTests
{
    private readonly InMemoryRepository<WidgetSettings> settings = new();
    private readonly InMemoryRepository<AthkarCategory> categories = new();
    private readonly InMemoryRepository<WidgetCatalogItem> catalog = new();
    private readonly InMemoryRepository<WidgetCatalogItemTranslation> translations = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly FakeAuditService audit = new();

    private WidgetService Service() =>
        new(settings, categories, catalog, translations, new FakeLanguageResolver(),
            unitOfWork, audit, new FakeSecurityManager());

    private static WidgetCatalogInput Valid(string key = "prayer_times") => new()
    {
        Key = key,
        Surface = WidgetSurface.Home,
        Family = WidgetFamily.Prayer,
        DesignCount = 3,
        DefaultDesign = 0,
        IsEnabled = true,
        Translations =
        [
            new TranslationInput { LanguageCode = "ar", Title = "مواقيت الصلاة" },
            new TranslationInput { LanguageCode = "en", Title = "Prayer times" },
        ],
    };

    [Fact]
    public async Task A_key_already_in_use_is_refused()
    {
        var service = Service();
        await service.CreateCatalogItem(Valid());

        var second = await service.CreateCatalogItem(Valid());

        Assert.False(second.Success);
        Assert.Equal(ErrorCode.DuplicateWidgetKey, second.ErrorCode);
    }

    [Fact]
    public async Task Keys_are_folded_to_lower_case_before_the_duplicate_check()
    {
        // Matched against the app's registry as literals, so "Prayer_Times" and
        // "prayer_times" cannot be allowed to be two different widgets.
        var service = Service();
        await service.CreateCatalogItem(Valid());

        var second = await service.CreateCatalogItem(Valid("  Prayer_Times  "));

        Assert.False(second.Success);
        Assert.Equal(ErrorCode.DuplicateWidgetKey, second.ErrorCode);
    }

    [Fact]
    public async Task A_default_design_outside_the_offered_range_is_refused()
    {
        var input = Valid();
        input.DesignCount = 2;
        input.DefaultDesign = 2;

        var response = await Service().CreateCatalogItem(input);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.WidgetDefaultDesignOutOfRange, response.ErrorCode);
    }

    [Fact]
    public async Task An_update_cannot_change_the_key()
    {
        var service = Service();
        var created = await service.CreateCatalogItem(Valid());

        var input = Valid("something_else");
        input.DesignCount = 1;
        input.DefaultDesign = 0;

        var updated = await service.UpdateCatalogItem(created.Data!.Id, input);

        Assert.True(updated.Success);
        Assert.Equal("prayer_times", updated.Data!.Key);
    }

    [Fact]
    public async Task A_hidden_entry_leaves_the_readers_gallery()
    {
        var service = Service();
        var created = await service.CreateCatalogItem(Valid());

        var input = Valid();
        input.IsEnabled = false;
        await service.UpdateCatalogItem(created.Data!.Id, input);

        var gallery = await service.Catalog("ar");

        Assert.True(gallery.Success);
        Assert.Empty(gallery.Data!.Items);
    }

    [Fact]
    public async Task Changing_the_gallery_moves_the_version_the_app_syncs_against()
    {
        var service = Service();
        var before = (await service.Get()).Data!.Version;

        await service.CreateCatalogItem(Valid());

        var after = (await service.Get()).Data!.Version;

        // Without this the app has no reason to re-fetch, and a withdrawn widget
        // sits in the reader's gallery until something unrelated bumps it.
        Assert.True(after > before, $"version did not move: {before} → {after}");
    }

    [Fact]
    public async Task An_entry_with_no_readable_wording_is_dropped_rather_than_shown_under_its_key()
    {
        var input = Valid();
        input.Translations = [new TranslationInput { LanguageCode = "tr", Title = "Namaz vakitleri" }];

        var service = Service();
        await service.CreateCatalogItem(input);

        var gallery = await service.Catalog("ar");

        Assert.Empty(gallery.Data!.Items);
    }

    [Fact]
    public async Task A_missing_translation_falls_back_to_the_default_language()
    {
        var input = Valid();
        input.Translations = [new TranslationInput { LanguageCode = "ar", Title = "مواقيت الصلاة" }];

        var service = Service();
        await service.CreateCatalogItem(input);

        var gallery = await service.Catalog("en");

        Assert.Equal("مواقيت الصلاة", Assert.Single(gallery.Data!.Items).Title);
    }

    [Fact]
    public async Task A_reorder_naming_an_unknown_id_changes_nothing()
    {
        var service = Service();
        var first = (await service.CreateCatalogItem(Valid("prayer_times"))).Data!;
        var second = (await service.CreateCatalogItem(Valid("date_only"))).Data!;

        var response = await service.ReorderCatalog(new WidgetCatalogReorderInput
        {
            Ids = [second.Id, first.Id, 9999],
        });

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.WidgetCatalogItemNotFound, response.ErrorCode);

        // The refusal is the point: a partial reorder writes positions derived
        // from a list the admin was never looking at.
        var gallery = await service.ListCatalog();
        Assert.Equal(first.Id, gallery.Data![0].Id);
    }

    [Fact]
    public async Task A_reorder_writes_the_order_the_admin_dragged_into()
    {
        var service = Service();
        var first = (await service.CreateCatalogItem(Valid("prayer_times"))).Data!;
        var second = (await service.CreateCatalogItem(Valid("date_only"))).Data!;

        var response = await service.ReorderCatalog(new WidgetCatalogReorderInput
        {
            Ids = [second.Id, first.Id],
        });

        Assert.True(response.Success);

        var gallery = (await service.ListCatalog()).Data!;
        Assert.Equal(second.Id, gallery[0].Id);
        Assert.Equal(first.Id, gallery[1].Id);
    }

    [Fact]
    public async Task Withdrawing_a_language_removes_that_wording_rather_than_leaving_it_stale()
    {
        var service = Service();
        var created = await service.CreateCatalogItem(Valid());

        var input = Valid();
        input.Translations = [new TranslationInput { LanguageCode = "ar", Title = "مواقيت الصلاة" }];
        await service.UpdateCatalogItem(created.Data!.Id, input);

        var gallery = await service.ListCatalog();
        var wording = Assert.Single(gallery.Data!).Translations;

        Assert.Equal("ar", Assert.Single(wording).LanguageCode);
    }

    // ───────────────────────── the default entry ─────────────────────────

    private static WidgetSettingsInput Rules(string? defaultKey = null) => new()
    {
        IsEnabled = true,
        DefaultKind = WidgetKind.NextPrayer,
        AllowPrayerWidget = true,
        AllowDhikrWidget = true,
        Theme = WidgetTheme.System,
        RefreshMinutes = 30,
        ShowHijriDate = true,
        ShowCountdown = true,
        CustomWidgetMaxLength = 280,
        DefaultWidgetKey = defaultKey,
    };

    [Fact]
    public async Task A_default_naming_no_gallery_entry_is_refused()
    {
        // Corrected silently, every fresh install would open on some other
        // widget and nobody would see it happen.
        var response = await Service().Update(Rules("a_widget_that_does_not_exist"));

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.WidgetDefaultUnknown, response.ErrorCode);
    }

    [Fact]
    public async Task A_default_naming_a_hidden_entry_is_refused()
    {
        var service = Service();
        var input = Valid();
        input.IsEnabled = false;
        await service.CreateCatalogItem(input);

        var response = await service.Update(Rules("prayer_times"));

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.WidgetDefaultUnknown, response.ErrorCode);
    }

    [Fact]
    public async Task A_default_naming_an_offered_entry_is_kept()
    {
        var service = Service();
        await service.CreateCatalogItem(Valid());

        var response = await service.Update(Rules("  Prayer_Times  "));

        Assert.True(response.Success);
        // Folded on the way in, because the app matches it against its renderer
        // registry as a literal.
        Assert.Equal("prayer_times", response.Data!.DefaultWidgetKey);
    }

    [Fact]
    public async Task Hiding_the_default_entry_clears_the_default()
    {
        var service = Service();
        var created = await service.CreateCatalogItem(Valid());
        await service.Update(Rules("prayer_times"));

        var hide = Valid();
        hide.IsEnabled = false;
        await service.UpdateCatalogItem(created.Data!.Id, hide);

        // Left pointing at a withdrawn widget, every fresh install would open on
        // something the gallery no longer offers.
        Assert.Null((await service.Get()).Data!.DefaultWidgetKey);
    }

    [Fact]
    public async Task Deleting_the_default_entry_clears_the_default()
    {
        var service = Service();
        var created = await service.CreateCatalogItem(Valid());
        await service.Update(Rules("prayer_times"));

        await service.DeleteCatalogItem(created.Data!.Id);

        Assert.Null((await service.Get()).Data!.DefaultWidgetKey);
    }

    [Fact]
    public async Task Clearing_the_default_is_allowed_and_means_whatever_the_app_can_draw()
    {
        var service = Service();
        await service.CreateCatalogItem(Valid());
        await service.Update(Rules("prayer_times"));

        var response = await service.Update(Rules(""));

        Assert.True(response.Success);
        Assert.Null(response.Data!.DefaultWidgetKey);
    }

    [Fact]
    public async Task The_gallery_carries_the_rules_it_is_browsed_under()
    {
        // One call rather than two: neither half is any use without the other,
        // and the app fetches this on every sync.
        var response = await Service().Catalog("ar");

        Assert.True(response.Success);
        Assert.True(response.Data!.Settings.AllowCustomWidget);
        Assert.Equal(280, response.Data.Settings.CustomWidgetMaxLength);
    }
}
