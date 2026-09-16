using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Content;
using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Widgets.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Models;
using Athkar.Areas.Domain.Widgets;
using Athkar.Areas.Services.Content.Models;
using Athkar.Areas.Services.Localization;
using Athkar.Shareds.Security;
using Entity = Athkar.Areas.Domain.Widgets.WidgetSettings;

namespace Athkar.Areas.Services.Widgets;

/// <summary>
/// The single widget-settings row.
///
/// Same shape as <c>AppConfigurationService</c>: seeded on startup, but
/// <see cref="Current"/> still tolerates its absence, because the app asks for
/// this during launch and a missing row must degrade to the defaults rather
/// than fail.
/// </summary>
public class WidgetService : IWidgetService
{
    private readonly IRepository<Entity> repository;
    private readonly IRepository<AthkarCategory> categories;
    private readonly IRepository<WidgetCatalogItem> catalog;
    private readonly IRepository<WidgetCatalogItemTranslation> catalogTranslations;
    private readonly ILanguageResolver languages;
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly ISecurityManager securityManager;

    public WidgetService(
        IRepository<Entity> repository,
        IRepository<AthkarCategory> categories,
        IRepository<WidgetCatalogItem> catalog,
        IRepository<WidgetCatalogItemTranslation> catalogTranslations,
        ILanguageResolver languages,
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        ISecurityManager securityManager)
    {
        this.repository = repository;
        this.categories = categories;
        this.catalog = catalog;
        this.catalogTranslations = catalogTranslations;
        this.languages = languages;
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.securityManager = securityManager;
    }

    public async Task<BaseResponse<WidgetSettingsOutput>> Get()
    {
        var entity = await Current() ?? new Entity();
        return new BaseResponse<WidgetSettingsOutput>(new WidgetSettingsOutput(entity));
    }

    public async Task<BaseResponse<WidgetSettingsOutput>> Update(WidgetSettingsInput input)
    {
        // Refused rather than quietly corrected: an admin who has turned off
        // both kinds has almost certainly not meant to leave the reader with a
        // widget picker containing nothing. The master switch is how you turn
        // widgets off, and it says so.
        if (input.IsEnabled && !input.AllowPrayerWidget && !input.AllowDhikrWidget)
            return BaseResponse<WidgetSettingsOutput>.Fail(ErrorCode.WidgetNoKindAllowed);

        if (input.DhikrCategoryId is { } categoryId &&
            !await categories.AnyAsync(c => c.Id == categoryId))
            return BaseResponse<WidgetSettingsOutput>.Fail(ErrorCode.CategoryNotFound);

        var defaultKey = NormaliseKey(input.DefaultWidgetKey ?? string.Empty);

        // An entry that is hidden counts as absent: a default pointing at a
        // withdrawn widget would put every fresh install on something the
        // gallery does not offer, and nobody would see it happen.
        if (defaultKey.Length > 0 &&
            !await catalog.AnyAsync(x => x.Key == defaultKey && x.IsEnabled))
            return BaseResponse<WidgetSettingsOutput>.Fail(ErrorCode.WidgetDefaultUnknown);

        var entity = await Current();
        var isNew = entity is null;
        entity ??= new Entity();

        var before = new WidgetSettingsOutput(entity);

        entity.IsEnabled = input.IsEnabled;
        entity.AllowPrayerWidget = input.AllowPrayerWidget;
        entity.AllowDhikrWidget = input.AllowDhikrWidget;
        // Forced back to one that is actually offered, so the app never opens
        // its picker on a kind the admin has withdrawn.
        entity.DefaultKind = ResolveDefault(input);
        entity.Theme = input.Theme;
        entity.RefreshMinutes = Math.Clamp(
            input.RefreshMinutes, WidgetRules.MinRefreshMinutes, WidgetRules.MaxRefreshMinutes);
        entity.ShowHijriDate = input.ShowHijriDate;
        entity.ShowCountdown = input.ShowCountdown;
        entity.DhikrCategoryId = input.DhikrCategoryId;
        entity.AllowBackgroundColor = input.AllowBackgroundColor;
        entity.AllowTransparency = input.AllowTransparency;
        entity.AllowBackgroundImage = input.AllowBackgroundImage;
        entity.AllowCustomWidget = input.AllowCustomWidget;
        entity.CustomWidgetMaxLength = Math.Clamp(
            input.CustomWidgetMaxLength, WidgetRules.MinCustomLength, WidgetRules.MaxCustomLength);
        entity.DefaultWidgetKey = defaultKey.Length == 0 ? null : defaultKey;
        entity.Version++;
        entity.ModifiedBy = securityManager.UserId;

        if (isNew) repository.Create(entity);
        else repository.Update(entity);

        await unitOfWork.SaveAsync();

        await auditService.LogAsync(AuditActions.WidgetUpdate, nameof(Entity), entity.Id,
            before, new WidgetSettingsOutput(entity));

        return new BaseResponse<WidgetSettingsOutput>(new WidgetSettingsOutput(entity));
    }

    /// <summary>
    /// The default kind, corrected to one the admin actually allows.
    ///
    /// Without this, withdrawing the prayer widget while it is still the
    /// default leaves every fresh install choosing something it cannot have.
    /// </summary>
    private static Shareds.Enums.WidgetKind ResolveDefault(WidgetSettingsInput input)
    {
        return input.DefaultKind switch
        {
            Shareds.Enums.WidgetKind.NextPrayer when !input.AllowPrayerWidget =>
                Shareds.Enums.WidgetKind.Dhikr,
            Shareds.Enums.WidgetKind.Dhikr when !input.AllowDhikrWidget =>
                Shareds.Enums.WidgetKind.NextPrayer,
            Shareds.Enums.WidgetKind.Combined when !input.AllowPrayerWidget =>
                Shareds.Enums.WidgetKind.Dhikr,
            Shareds.Enums.WidgetKind.Combined when !input.AllowDhikrWidget =>
                Shareds.Enums.WidgetKind.NextPrayer,
            _ => input.DefaultKind,
        };
    }

    /// <summary>Oldest live row wins, so a stray duplicate cannot flip the rules.</summary>
    private Task<Entity?> Current() =>
        repository.Query().OrderBy(x => x.Id).FirstOrDefaultAsync();

    // ─────────────────────────── the gallery ───────────────────────────

    public async Task<BaseResponse<WidgetCatalogBundleOutput>> Catalog(string? languageCode)
    {
        var language = await languages.Resolve(languageCode);
        var fallback = await languages.Default();

        var rows = await catalog.Query()
            .Where(x => x.IsEnabled)
            .Include(x => x.Translations)
            .OrderBy(x => x.Surface).ThenBy(x => x.SortOrder).ThenBy(x => x.Id)
            .ToListAsync();

        var settings = await Current() ?? new Entity();

        var items = rows
            .Select(item =>
            {
                var live = item.Translations.Where(t => !t.IsDeleted).ToList();

                // An entry with no wording in either the reader's language or
                // the default is dropped rather than shown under its key. A key
                // is a developer's handle; nobody should meet one in a gallery.
                var wording = live.FirstOrDefault(t => t.LanguageCode == language)
                              ?? live.FirstOrDefault(t => t.LanguageCode == fallback);

                return wording is null ? null : new WidgetCatalogOutput
                {
                    Id = item.Id,
                    Key = item.Key,
                    Surface = item.Surface,
                    Family = item.Family,
                    Title = wording.Title,
                    Subtitle = wording.Subtitle,
                    DesignCount = item.DesignCount,
                    DefaultDesign = item.DefaultDesign,
                    IsExclusive = item.IsExclusive,
                    IsNew = item.IsNew,
                    SortOrder = item.SortOrder,
                };
            })
            .OfType<WidgetCatalogOutput>()
            .ToList();

        return new BaseResponse<WidgetCatalogBundleOutput>(new WidgetCatalogBundleOutput
        {
            Settings = new WidgetSettingsOutput(settings),
            Items = items,
        });
    }

    public async Task<BaseResponse<List<AdminWidgetCatalogOutput>>> ListCatalog()
    {
        var rows = await catalog.Query()
            .Include(x => x.Translations)
            .OrderBy(x => x.Surface).ThenBy(x => x.SortOrder).ThenBy(x => x.Id)
            .ToListAsync();

        return new BaseResponse<List<AdminWidgetCatalogOutput>>(
            [.. rows.Select(x => new AdminWidgetCatalogOutput(x))]);
    }

    public async Task<BaseResponse<AdminWidgetCatalogOutput>> CreateCatalogItem(WidgetCatalogInput input)
    {
        var key = NormaliseKey(input.Key);

        if (await catalog.AnyAsync(x => x.Key == key))
            return BaseResponse<AdminWidgetCatalogOutput>.Fail(ErrorCode.DuplicateWidgetKey);

        if (input.DefaultDesign >= input.DesignCount)
            return BaseResponse<AdminWidgetCatalogOutput>.Fail(ErrorCode.WidgetDefaultDesignOutOfRange);

        var item = new WidgetCatalogItem
        {
            Key = key,
            Surface = input.Surface,
            Family = input.Family,
            DesignCount = input.DesignCount,
            DefaultDesign = input.DefaultDesign,
            IsExclusive = input.IsExclusive,
            IsNew = input.IsNew,
            IsEnabled = input.IsEnabled,
            SortOrder = input.SortOrder,
            CreatedBy = securityManager.UserId,
        };

        foreach (var translation in Distinct(input.Translations))
            item.Translations.Add(Build(translation));

        await catalog.AddAsync(item);
        await BumpVersion();
        await unitOfWork.SaveAsync();

        var output = new AdminWidgetCatalogOutput(item);
        await auditService.LogAsync(
            AuditActions.WidgetCatalogCreate, nameof(WidgetCatalogItem), item.Id, null, output);

        return new BaseResponse<AdminWidgetCatalogOutput>(output);
    }

    public async Task<BaseResponse<AdminWidgetCatalogOutput>> UpdateCatalogItem(
        int id, WidgetCatalogInput input)
    {
        var item = await catalog.Query()
            .Include(x => x.Translations)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item is null)
            return BaseResponse<AdminWidgetCatalogOutput>.Fail(ErrorCode.WidgetCatalogItemNotFound);

        if (input.DefaultDesign >= input.DesignCount)
            return BaseResponse<AdminWidgetCatalogOutput>.Fail(ErrorCode.WidgetDefaultDesignOutOfRange);

        var before = new AdminWidgetCatalogOutput(item);

        // The key is deliberately not taken from the payload. Every phone that
        // has placed this widget is holding the old key, and renaming it here
        // would not rename it there — it would simply make the entry stop
        // matching anything the app can draw. A key that must change is a new
        // entry and a withdrawn one.
        item.Surface = input.Surface;
        item.Family = input.Family;
        item.DesignCount = input.DesignCount;
        item.DefaultDesign = input.DefaultDesign;
        item.IsExclusive = input.IsExclusive;
        item.IsNew = input.IsNew;
        item.IsEnabled = input.IsEnabled;
        item.SortOrder = input.SortOrder;
        item.ModifiedBy = securityManager.UserId;
        catalog.Update(item);

        var incoming = Distinct(input.Translations)
            .ToDictionary(t => t.LanguageCode.Trim().ToLowerInvariant());

        foreach (var existing in item.Translations.Where(t => !t.IsDeleted).ToList())
        {
            if (incoming.TryGetValue(existing.LanguageCode, out var update))
            {
                existing.Title = update.Title.Trim();
                existing.Subtitle = Clean(update.Body);
                catalogTranslations.Update(existing);
                incoming.Remove(existing.LanguageCode);
            }
            else
            {
                catalogTranslations.SoftDelete(existing);
            }
        }

        foreach (var added in incoming.Values)
            item.Translations.Add(Build(added));

        // Hiding the entry that is the default would leave every fresh install
        // pointed at a widget the gallery no longer offers.
        if (!item.IsEnabled) await ClearDefaultPointingAt(item.Key);

        await BumpVersion();
        await unitOfWork.SaveAsync();

        var output = new AdminWidgetCatalogOutput(item);
        await auditService.LogAsync(
            AuditActions.WidgetCatalogUpdate, nameof(WidgetCatalogItem), item.Id, before, output);

        return new BaseResponse<AdminWidgetCatalogOutput>(output);
    }

    public async Task<BaseResponse> DeleteCatalogItem(int id)
    {
        var item = await catalog.Query()
            .Include(x => x.Translations)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item is null) return BaseResponse.Fail(ErrorCode.WidgetCatalogItemNotFound);

        var before = new AdminWidgetCatalogOutput(item);

        catalog.SoftDelete(item);
        catalogTranslations.SoftDeleteRange(item.Translations.Where(t => !t.IsDeleted));

        await ClearDefaultPointingAt(item.Key);
        await BumpVersion();
        await unitOfWork.SaveAsync();

        await auditService.LogAsync(
            AuditActions.WidgetCatalogDelete, nameof(WidgetCatalogItem), item.Id, before, null);

        return new BaseResponse();
    }

    public async Task<BaseResponse> ReorderCatalog(WidgetCatalogReorderInput input)
    {
        var rows = await catalog.Query().Where(x => input.Ids.Contains(x.Id)).ToListAsync();

        // Every id or none: a partial reorder writes positions derived from a
        // list the admin was not looking at, and the result is a gallery that
        // rearranges itself for reasons nobody can reconstruct.
        if (rows.Count != input.Ids.Count)
            return BaseResponse.Fail(ErrorCode.WidgetCatalogItemNotFound);

        for (var index = 0; index < input.Ids.Count; index++)
        {
            var row = rows.First(x => x.Id == input.Ids[index]);
            row.SortOrder = index;
            row.ModifiedBy = securityManager.UserId;
            catalog.Update(row);
        }

        await BumpVersion();
        await unitOfWork.SaveAsync();

        await auditService.LogAsync(
            AuditActions.WidgetCatalogReorder, nameof(WidgetCatalogItem), null, null, input.Ids);

        return new BaseResponse();
    }

    /// <summary>
    /// Drops the default when the entry it names stops being offered.
    ///
    /// Back to null rather than to some other widget: null means "the first one
    /// the reader's app can draw", which is a rule, whereas picking a
    /// replacement here would be a decision, and decisions about what readers
    /// see belong to the admin.
    /// </summary>
    private async Task ClearDefaultPointingAt(string key)
    {
        var settings = await Current();
        if (settings?.DefaultWidgetKey != key) return;

        settings.DefaultWidgetKey = null;
        repository.Update(settings);
    }

    /// <summary>
    /// Moves the settings row's version on after a catalogue change.
    ///
    /// The version is how a device decides whether what it last pushed to the
    /// launcher is still current. Without this, hiding a widget would be a
    /// change no phone had any reason to notice — the settings it syncs against
    /// would look untouched — and the withdrawn entry would sit in a reader's
    /// gallery until something unrelated happened to bump it.
    /// </summary>
    private async Task BumpVersion()
    {
        var settings = await Current();

        // A missing row is created *already moved on*, not at the default. A
        // device that has never synced is holding version 1, so a row born at 1
        // would tell it nothing had changed on the very boot where the gallery
        // came into existence.
        settings ??= new Entity();
        settings.Version++;

        if (settings.Id == 0)
        {
            repository.Create(settings);
            return;
        }

        settings.ModifiedBy = securityManager.UserId;
        repository.Update(settings);
    }

    private static WidgetCatalogItemTranslation Build(TranslationInput input) => new()
    {
        LanguageCode = input.LanguageCode.Trim().ToLowerInvariant(),
        Title = input.Title.Trim(),
        Subtitle = Clean(input.Body),
    };

    /// <summary>Last wording for a language wins, rather than the save failing.</summary>
    private static IEnumerable<TranslationInput> Distinct(IEnumerable<TranslationInput> translations) =>
        translations
            .Where(t => !string.IsNullOrWhiteSpace(t.LanguageCode) && !string.IsNullOrWhiteSpace(t.Title))
            .GroupBy(t => t.LanguageCode.Trim().ToLowerInvariant())
            .Select(group => group.Last());

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Keys are matched against the app's renderer registry as literals, so the
    /// one place they can be normalised is before they are ever stored.
    /// </summary>
    private static string NormaliseKey(string key) => key.Trim().ToLowerInvariant();
}
