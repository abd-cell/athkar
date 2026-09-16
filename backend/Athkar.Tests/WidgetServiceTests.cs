using Athkar.Areas.Domain.Content;
using Athkar.Areas.Domain.Widgets;
using Athkar.Areas.Services.Widgets;
using Athkar.Areas.Services.Widgets.Models;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;
using Athkar.Tests.TestDoubles;

namespace Athkar.Tests;

/// <summary>
/// The admin's control over the home-screen widget.
///
/// The rule worth testing here is the one that is easy to get wrong and
/// invisible when it breaks: **a kind the admin withdraws must stop being the
/// default**. Miss it and every fresh install opens its picker on something it
/// cannot have — on a surface the reader sees without opening the app, so
/// nobody reports it.
/// </summary>
public class WidgetServiceTests
{
    private readonly InMemoryRepository<WidgetSettings> settings = new();
    private readonly InMemoryRepository<AthkarCategory> categories = new();
    private readonly InMemoryRepository<WidgetCatalogItem> catalog = new();
    private readonly InMemoryRepository<WidgetCatalogItemTranslation> catalogTranslations = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly FakeAuditService audit = new();

    private WidgetService Service() =>
        new(settings, categories, catalog, catalogTranslations, new FakeLanguageResolver(),
            unitOfWork, audit, new FakeSecurityManager());

    private static WidgetSettingsInput Valid() => new()
    {
        IsEnabled = true,
        DefaultKind = WidgetKind.NextPrayer,
        AllowPrayerWidget = true,
        AllowDhikrWidget = true,
        Theme = WidgetTheme.System,
        RefreshMinutes = 30,
        ShowHijriDate = true,
        ShowCountdown = true,
    };

    [Fact]
    public async Task Defaults_are_returned_when_nothing_has_been_configured()
    {
        // The app asks for this during launch, so an empty table must degrade to
        // the defaults rather than fail.
        var response = await Service().Get();

        Assert.True(response.Success);
        Assert.True(response.Data!.IsEnabled);
        Assert.Equal(WidgetKind.NextPrayer, response.Data.DefaultKind);
    }

    [Fact]
    public async Task Enabling_widgets_with_every_kind_withdrawn_is_refused()
    {
        var input = Valid();
        input.AllowPrayerWidget = false;
        input.AllowDhikrWidget = false;

        var response = await Service().Update(input);

        Assert.Equal(ErrorCode.WidgetNoKindAllowed, response.ErrorCode);
        Assert.Empty(settings.All);
    }

    [Fact]
    public async Task Withdrawing_every_kind_is_allowed_when_widgets_are_off()
    {
        // Not a contradiction: the master switch is how you turn widgets off,
        // and the kinds are then irrelevant rather than wrong.
        var input = Valid();
        input.IsEnabled = false;
        input.AllowPrayerWidget = false;
        input.AllowDhikrWidget = false;

        var response = await Service().Update(input);

        Assert.True(response.Success);
        Assert.False(response.Data!.IsEnabled);
    }

    [Theory]
    [InlineData(WidgetKind.NextPrayer, false, true, WidgetKind.Dhikr)]
    [InlineData(WidgetKind.Dhikr, true, false, WidgetKind.NextPrayer)]
    [InlineData(WidgetKind.Combined, false, true, WidgetKind.Dhikr)]
    [InlineData(WidgetKind.Combined, true, false, WidgetKind.NextPrayer)]
    public async Task The_default_kind_is_corrected_to_one_that_is_offered(
        WidgetKind asked, bool allowPrayer, bool allowDhikr, WidgetKind expected)
    {
        var input = Valid();
        input.DefaultKind = asked;
        input.AllowPrayerWidget = allowPrayer;
        input.AllowDhikrWidget = allowDhikr;

        var response = await Service().Update(input);

        Assert.True(response.Success);
        Assert.Equal(expected, response.Data!.DefaultKind);
    }

    [Fact]
    public async Task A_default_that_is_still_offered_is_left_alone()
    {
        var input = Valid();
        input.DefaultKind = WidgetKind.Dhikr;

        var response = await Service().Update(input);

        Assert.Equal(WidgetKind.Dhikr, response.Data!.DefaultKind);
    }

    [Theory]
    [InlineData(1, WidgetRules.MinRefreshMinutes)]
    [InlineData(100000, WidgetRules.MaxRefreshMinutes)]
    [InlineData(45, 45)]
    public async Task The_refresh_interval_is_clamped(int asked, int expected)
    {
        // Clamped here as well as validated at the controller: the attribute
        // answers the CMS, this answers everything else that can reach the row.
        var input = Valid();
        input.RefreshMinutes = asked;

        var response = await Service().Update(input);

        Assert.Equal(expected, response.Data!.RefreshMinutes);
    }

    [Fact]
    public async Task Pinning_a_chapter_that_does_not_exist_is_refused()
    {
        var input = Valid();
        input.DhikrCategoryId = 404;

        var response = await Service().Update(input);

        Assert.Equal(ErrorCode.CategoryNotFound, response.ErrorCode);
    }

    [Fact]
    public async Task A_chapter_may_be_pinned_and_unpinned()
    {
        var category = new AthkarCategory { Key = "morning" };
        categories.Seed(category);

        var service = Service();

        var pinned = Valid();
        pinned.DhikrCategoryId = category.Id;
        Assert.Equal(category.Id, (await service.Update(pinned)).Data!.DhikrCategoryId);

        // Null is the meaningful default — "follow the time of day" — not an
        // absence, so clearing it has to work.
        Assert.Null((await service.Update(Valid())).Data!.DhikrCategoryId);
    }

    [Fact]
    public async Task Every_change_moves_the_version_so_a_device_can_notice()
    {
        var service = Service();

        var first = await service.Update(Valid());
        var second = await service.Update(Valid());

        Assert.True(second.Data!.Version > first.Data!.Version);
    }

    [Fact]
    public async Task Changes_are_audited()
    {
        await Service().Update(Valid());

        Assert.Contains("admin.widget.update", audit.Actions);
    }

    [Fact]
    public async Task A_refused_change_writes_nothing_and_is_not_audited()
    {
        var input = Valid();
        input.AllowPrayerWidget = false;
        input.AllowDhikrWidget = false;

        await Service().Update(input);

        Assert.Empty(settings.All);
        Assert.Empty(audit.Actions);
    }
}
