using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Reminders;
using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Content.Models;
using Athkar.Areas.Services.Localization;
using Athkar.Areas.Services.Reminders.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Extensions;
using Athkar.Shareds.Models;
using Athkar.Shareds.Security;

namespace Athkar.Areas.Services.Reminders;

public class ReminderService : IReminderService
{
    private readonly IRepository<ReminderCampaign> campaigns;
    private readonly IRepository<ReminderCampaignTranslation> translations;
    private readonly IRepository<Domain.Content.AthkarCategory> categories;
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly ILanguageResolver languages;
    private readonly ISecurityManager securityManager;

    public ReminderService(
        IRepository<ReminderCampaign> campaigns,
        IRepository<ReminderCampaignTranslation> translations,
        IRepository<Domain.Content.AthkarCategory> categories,
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        ILanguageResolver languages,
        ISecurityManager securityManager)
    {
        this.campaigns = campaigns;
        this.translations = translations;
        this.categories = categories;
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.languages = languages;
        this.securityManager = securityManager;
    }

    public async Task<BaseResponse<PageOutput<ReminderOutput>>> List(PageInput input)
    {
        var query = campaigns.Query()
            .Include(c => c.Translations)
            .Include(c => c.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var term = input.Search.Trim();
            query = query.Where(c =>
                c.Key.Contains(term) ||
                c.Translations.Any(t => !t.IsDeleted && t.Title.Contains(term)));
        }

        var total = await query.CountAsync();
        var rows = await query.OrderBy(c => c.Key).Paginate(input).ToListAsync();

        return new BaseResponse<PageOutput<ReminderOutput>>(new PageOutput<ReminderOutput>
        {
            TotalRows = total,
            Data = [.. rows.Select(c => new ReminderOutput(c))],
        });
    }

    public async Task<BaseResponse<ReminderOutput>> Get(int id)
    {
        var campaign = await Load(id);
        return campaign is null
            ? BaseResponse<ReminderOutput>.Fail(ErrorCode.ReminderNotFound)
            : new BaseResponse<ReminderOutput>(new ReminderOutput(campaign));
    }

    public async Task<BaseResponse<ReminderOutput>> Create(ReminderInput input)
    {
        var key = input.Key.Trim().ToLowerInvariant();

        if (await campaigns.AnyAsync(c => c.Key == key))
            return BaseResponse<ReminderOutput>.Fail(ErrorCode.DuplicateKey);

        if (Validate(input) is { } failure)
            return BaseResponse<ReminderOutput>.Fail(failure);

        if (input.CategoryId is { } categoryId && !await categories.AnyAsync(c => c.Id == categoryId))
            return BaseResponse<ReminderOutput>.Fail(ErrorCode.CategoryNotFound);

        var campaign = new ReminderCampaign { Key = key, CreatedBy = securityManager.UserId };
        Apply(campaign, input);

        foreach (var translation in DistinctTranslations(input.Translations))
            campaign.Translations.Add(new ReminderCampaignTranslation
            {
                LanguageCode = translation.LanguageCode.Trim().ToLowerInvariant(),
                Title = translation.Title.Trim(),
                Body = (translation.Body ?? string.Empty).Trim(),
            });

        await campaigns.AddAsync(campaign);
        await unitOfWork.SaveAsync();

        var output = new ReminderOutput(campaign);
        await auditService.LogAsync(AuditActions.ReminderCreate, nameof(ReminderCampaign),
            campaign.Id, null, output);

        return new BaseResponse<ReminderOutput>(output);
    }

    public async Task<BaseResponse<ReminderOutput>> Update(int id, ReminderInput input)
    {
        var campaign = await Load(id);
        if (campaign is null) return BaseResponse<ReminderOutput>.Fail(ErrorCode.ReminderNotFound);

        var key = input.Key.Trim().ToLowerInvariant();
        if (key != campaign.Key && await campaigns.AnyAsync(c => c.Key == key))
            return BaseResponse<ReminderOutput>.Fail(ErrorCode.DuplicateKey);

        if (Validate(input) is { } failure)
            return BaseResponse<ReminderOutput>.Fail(failure);

        if (input.CategoryId is { } categoryId && !await categories.AnyAsync(c => c.Id == categoryId))
            return BaseResponse<ReminderOutput>.Fail(ErrorCode.CategoryNotFound);

        var before = new ReminderOutput(campaign);

        campaign.Key = key;
        Apply(campaign, input);
        // Every edit moves the version, which is how a device-local reminder
        // that is already scheduled on a phone learns it has been re-worded.
        campaign.Version++;
        campaign.ModifiedBy = securityManager.UserId;
        campaigns.Update(campaign);

        SyncTranslations(campaign, input.Translations);

        await unitOfWork.SaveAsync();

        var output = new ReminderOutput(campaign);
        await auditService.LogAsync(AuditActions.ReminderUpdate, nameof(ReminderCampaign),
            campaign.Id, before, output);

        return new BaseResponse<ReminderOutput>(output);
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var campaign = await Load(id);
        if (campaign is null) return BaseResponse.Fail(ErrorCode.ReminderNotFound);

        translations.SoftDeleteRange(campaign.Translations.Where(t => !t.IsDeleted));
        campaigns.SoftDelete(campaign);

        await unitOfWork.SaveAsync();

        await auditService.LogAsync(AuditActions.ReminderDelete, nameof(ReminderCampaign),
            id, new ReminderOutput(campaign), null);

        return new BaseResponse();
    }

    public async Task<BaseResponse<List<DeviceReminderOutput>>> ForDevice(string? languageCode)
    {
        var language = await languages.Resolve(languageCode);
        var fallback = await languages.Default();

        var rows = await campaigns.Query()
            .Where(c => c.IsEnabled && c.Delivery == ReminderDelivery.DeviceLocal)
            .Where(c => c.Audience == Audience.All ||
                        (c.Audience == Audience.Language && c.TargetLanguageCode == language))
            .Include(c => c.Translations)
            .OrderBy(c => c.Key)
            .ToListAsync();

        var output = rows
            .Select(campaign =>
            {
                var live = campaign.Translations.Where(t => !t.IsDeleted).ToList();
                var wording = live.FirstOrDefault(t => t.LanguageCode == language)
                              ?? live.FirstOrDefault(t => t.LanguageCode == fallback)
                              ?? live.FirstOrDefault();

                // A campaign with no wording at all has nothing to show; it is
                // dropped rather than sent as an empty notification.
                return wording is null ? null : new DeviceReminderOutput
                {
                    Id = campaign.Id,
                    Key = campaign.Key,
                    CategoryId = campaign.CategoryId,
                    Kind = campaign.Kind,
                    LocalTime = campaign.LocalTime,
                    Anchor = campaign.Anchor,
                    OffsetMinutes = campaign.OffsetMinutes,
                    Days = campaign.Days,
                    IsUserAdjustable = campaign.IsUserAdjustable,
                    AndroidChannelId = campaign.AndroidChannelId,
                    Version = campaign.Version,
                    Title = wording.Title,
                    Body = wording.Body,
                };
            })
            .OfType<DeviceReminderOutput>()
            .ToList();

        return new BaseResponse<List<DeviceReminderOutput>>(output);
    }

    /// <summary>
    /// The two ways a campaign can describe a schedule that never happens: no
    /// days, or a kind whose defining field is missing.
    /// </summary>
    private static ErrorCode? Validate(ReminderInput input)
    {
        if (input.Days == WeekDays.None) return ErrorCode.ReminderHasNoDays;

        // A channel id the app has never created is refused here rather than on
        // the phone, where Android would drop or silently rehome the
        // notification and nobody could see that it had happened.
        if (!string.IsNullOrWhiteSpace(input.AndroidChannelId) &&
            !PushRules.Channels.All.Contains(input.AndroidChannelId.Trim()))
        {
            return ErrorCode.UnknownNotificationChannel;
        }

        return input.Kind switch
        {
            ReminderKind.FixedTime when input.LocalTime is null => ErrorCode.ReminderScheduleIncomplete,
            ReminderKind.PrayerAnchored when input.Anchor == PrayerAnchor.None => ErrorCode.ReminderScheduleIncomplete,
            _ => null,
        };
    }

    /// <summary>
    /// Copies the input onto the entity, and settles the one combination the CMS
    /// must not be able to produce: a prayer-anchored campaign pushed from the
    /// server. The server cannot know when Maghrib is for a reader whose
    /// coordinates it deliberately never receives, so an anchored campaign is
    /// forced to device-local delivery rather than silently never firing.
    /// </summary>
    private static void Apply(ReminderCampaign campaign, ReminderInput input)
    {
        campaign.CategoryId = input.CategoryId;
        campaign.Kind = input.Kind;
        campaign.Delivery = input.Kind == ReminderKind.PrayerAnchored
            ? ReminderDelivery.DeviceLocal
            : input.Delivery;
        campaign.LocalTime = input.Kind == ReminderKind.FixedTime ? input.LocalTime : null;
        campaign.Anchor = input.Kind == ReminderKind.PrayerAnchored ? input.Anchor : PrayerAnchor.None;
        campaign.OffsetMinutes = input.Kind == ReminderKind.PrayerAnchored ? input.OffsetMinutes : 0;
        campaign.Days = input.Days;
        campaign.Audience = input.Audience;
        campaign.TargetLanguageCode = input.Audience == Audience.Language
            ? input.TargetLanguageCode?.Trim().ToLowerInvariant()
            : null;
        campaign.TargetPlatform = input.Audience == Audience.Platform ? input.TargetPlatform : null;
        campaign.IsEnabled = input.IsEnabled;
        campaign.IsUserAdjustable = input.IsUserAdjustable;

        campaign.AndroidChannelId = string.IsNullOrWhiteSpace(input.AndroidChannelId)
            ? PushRules.Channels.Reminders
            : input.AndroidChannelId.Trim();
    }

    private Task<ReminderCampaign?> Load(int id) =>
        campaigns.Query()
            .Include(c => c.Translations)
            .Include(c => c.Category)
            .FirstOrDefaultAsync(c => c.Id == id);

    private static IEnumerable<TranslationInput> DistinctTranslations(IEnumerable<TranslationInput> input) =>
        input
            .Where(t => !string.IsNullOrWhiteSpace(t.LanguageCode) && !string.IsNullOrWhiteSpace(t.Title))
            .GroupBy(t => t.LanguageCode.Trim().ToLowerInvariant())
            .Select(g => g.Last());

    private void SyncTranslations(ReminderCampaign campaign, List<TranslationInput> wanted)
    {
        var incoming = DistinctTranslations(wanted)
            .ToDictionary(t => t.LanguageCode.Trim().ToLowerInvariant());

        foreach (var existing in campaign.Translations.Where(t => !t.IsDeleted).ToList())
        {
            if (incoming.TryGetValue(existing.LanguageCode, out var update))
            {
                existing.Title = update.Title.Trim();
                existing.Body = (update.Body ?? string.Empty).Trim();
                translations.Update(existing);
                incoming.Remove(existing.LanguageCode);
            }
            else
            {
                translations.SoftDelete(existing);
            }
        }

        foreach (var (code, translation) in incoming)
            campaign.Translations.Add(new ReminderCampaignTranslation
            {
                CampaignId = campaign.Id,
                LanguageCode = code,
                Title = translation.Title.Trim(),
                Body = (translation.Body ?? string.Empty).Trim(),
            });
    }
}
