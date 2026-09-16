using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Configuration;
using Athkar.Areas.Domain.Devices;
using Athkar.Areas.Domain.Quran;
using Athkar.Areas.Services.Devices.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Extensions;
using Athkar.Shareds.Models;
using Athkar.Shareds.Security;

namespace Athkar.Areas.Services.Devices;

/// <summary>
/// Everything the server knows about a reader passes through here.
///
/// Registration is an upsert keyed on the device key, which means the app never
/// has to remember whether it has registered and a reinstall simply becomes a
/// new row. Nothing in this service reads or writes anything that could identify
/// a person.
/// </summary>
public class DeviceService : IDeviceService
{
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromDays(30);

    private readonly IRepository<Device> devices;
    private readonly IRepository<DeviceNotification> notifications;
    private readonly IRepository<AppConfiguration> configurations;
    private readonly IRepository<QuranPackage> packages;
    private readonly IUnitOfWork unitOfWork;
    private readonly IDeviceContext deviceContext;

    public DeviceService(
        IRepository<Device> devices,
        IRepository<DeviceNotification> notifications,
        IRepository<AppConfiguration> configurations,
        IRepository<QuranPackage> packages,
        IUnitOfWork unitOfWork,
        IDeviceContext deviceContext)
    {
        this.devices = devices;
        this.notifications = notifications;
        this.configurations = configurations;
        this.packages = packages;
        this.unitOfWork = unitOfWork;
        this.deviceContext = deviceContext;
    }

    public async Task<BaseResponse<DeviceOutput>> Register(DeviceRegistrationInput input)
    {
        var key = input.DeviceKey.Trim();
        if (!Guid.TryParse(key, out _))
            return BaseResponse<DeviceOutput>.Fail(ErrorCode.InvalidDeviceKey);

        // A device that was forgotten and comes back is the same install, so the
        // deleted row is revived rather than duplicated — the alternative leaves
        // the unique index to reject the registration of an app that is, from
        // the reader's side, simply being opened again.
        var device = await devices.FirstOrDefaultAsync(d => d.DeviceKey == key, includeDeleted: true);
        var isNew = device is null;

        device ??= new Device { DeviceKey = key };

        device.IsDeleted = false;
        device.DeletionDate = null;
        device.Platform = input.Platform;
        device.PushToken = string.IsNullOrWhiteSpace(input.PushToken) ? null : input.PushToken.Trim();
        device.LanguageCode = input.LanguageCode.Trim().ToLowerInvariant();
        device.TimeZoneId = ResolveTimeZone(input.TimeZoneId);
        device.NotificationsEnabled = input.NotificationsEnabled;
        device.AppVersion = Blank(input.AppVersion);
        device.CountryCode = Blank(input.CountryCode)?.ToUpperInvariant();
        device.LastSeenAt = DateTime.UtcNow;

        if (isNew) await devices.AddAsync(device);
        else devices.Update(device);

        await unitOfWork.SaveAsync();

        return new BaseResponse<DeviceOutput>(new DeviceOutput
        {
            DeviceKey = device.DeviceKey,
            LanguageCode = device.LanguageCode,
            TimeZoneId = device.TimeZoneId,
            NotificationsEnabled = device.NotificationsEnabled,
            ContentVersion = await CurrentContentVersion(),
            QuranPackageVersion = await PublishedQuranVersion(),
            UnreadNotifications = await notifications
                .CountAsync(n => n.DeviceId == device.Id && n.ReadAt == null),
        });
    }

    public async Task<BaseResponse> UpdatePushToken(PushTokenInput input)
    {
        var key = input.DeviceKey.Trim();
        if (!Guid.TryParse(key, out _))
            return BaseResponse.Fail(ErrorCode.InvalidDeviceKey);

        var device = await devices.FirstOrDefaultAsync(d => d.DeviceKey == key);

        // Not an upsert, unlike Register. A token arriving for a key the server
        // has never seen is a refresh that raced the first registration, and
        // creating a half-populated row from it would leave a device with no
        // language and no timezone in every future fan-out. The app registers on
        // every launch, so the token lands a moment later anyway.
        if (device is null) return BaseResponse.Fail(ErrorCode.DeviceNotFound);

        var token = string.IsNullOrWhiteSpace(input.PushToken) ? null : input.PushToken.Trim();

        // LastSeenAt is deliberately not touched: a token rotating says the OS
        // is awake, not that the reader opened the app, and the active-installs
        // figure means the second of those.
        device.PushToken = token;
        device.NotificationsEnabled = input.NotificationsEnabled;

        devices.Update(device);
        await unitOfWork.SaveAsync();

        return new BaseResponse();
    }

    public async Task<BaseResponse<PageOutput<NotificationOutput>>> Notifications(PageInput input)
    {
        var device = await RequireDevice();
        if (device is null)
            return BaseResponse<PageOutput<NotificationOutput>>.Fail(ErrorCode.DeviceNotFound);

        var query = notifications.Where(n => n.DeviceId == device.Id);

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(n => n.CreationDate)
            .Paginate(input)
            .ToListAsync();

        return new BaseResponse<PageOutput<NotificationOutput>>(new PageOutput<NotificationOutput>
        {
            Data = [.. rows.Select(n => new NotificationOutput(n))],
            TotalRows = total,
        });
    }

    public async Task<BaseResponse> MarkRead(int notificationId)
    {
        var device = await RequireDevice();
        if (device is null) return BaseResponse.Fail(ErrorCode.DeviceNotFound);

        var row = await notifications.FirstOrDefaultAsync(
            n => n.Id == notificationId && n.DeviceId == device.Id);

        if (row is null) return BaseResponse.Fail(ErrorCode.NotFound);

        if (row.ReadAt is null)
        {
            row.ReadAt = DateTime.UtcNow;
            notifications.Update(row);
            await unitOfWork.SaveAsync();
        }

        return new BaseResponse();
    }

    public async Task<BaseResponse> MarkAllRead()
    {
        var device = await RequireDevice();
        if (device is null) return BaseResponse.Fail(ErrorCode.DeviceNotFound);

        var unread = await notifications
            .Where(n => n.DeviceId == device.Id && n.ReadAt == null)
            .ToListAsync();

        foreach (var row in unread)
        {
            row.ReadAt = DateTime.UtcNow;
            notifications.Update(row);
        }

        await unitOfWork.SaveAsync();
        return new BaseResponse();
    }

    public async Task<BaseResponse> Forget()
    {
        var device = await RequireDevice();
        if (device is null) return BaseResponse.Fail(ErrorCode.DeviceNotFound);

        var inbox = await notifications.Where(n => n.DeviceId == device.Id).ToListAsync();
        notifications.SoftDeleteRange(inbox);

        // Cleared, not merely soft-deleted: a stale token in a deleted row is
        // still a token, and the sender reads rows it was handed rather than
        // re-checking why they are there.
        device.PushToken = null;
        device.NotificationsEnabled = false;
        devices.SoftDelete(device);

        await unitOfWork.SaveAsync();
        return new BaseResponse();
    }

    public async Task<BaseResponse<DeviceStatsOutput>> Stats()
    {
        var since = DateTime.UtcNow - ActiveWindow;
        var all = devices.Query();

        var stats = new DeviceStatsOutput
        {
            TotalDevices = await all.CountAsync(),
            ActiveDevices = await all.CountAsync(d => d.LastSeenAt >= since),
            ReachableDevices = await all.CountAsync(d =>
                d.NotificationsEnabled && d.PushToken != null),

            ByPlatform = await all
                .GroupBy(d => d.Platform)
                .Select(g => new { Key = g.Key.ToString(), Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count),

            ByLanguage = await all
                .GroupBy(d => d.LanguageCode)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count),

            ByCountry = await all
                .Where(d => d.CountryCode != null)
                .GroupBy(d => d.CountryCode!)
                .OrderByDescending(g => g.Count())
                .Take(20)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count),
        };

        return new BaseResponse<DeviceStatsOutput>(stats);
    }

    private async Task<Device?> RequireDevice()
    {
        var key = deviceContext.RequireDeviceKey();
        return await devices.FirstOrDefaultAsync(d => d.DeviceKey == key);
    }

    private async Task<int> CurrentContentVersion()
    {
        var configuration = await configurations.Query().OrderBy(c => c.Id).FirstOrDefaultAsync();
        return configuration?.ContentVersion ?? 1;
    }

    private async Task<int?> PublishedQuranVersion() =>
        await packages.Query()
            .Where(p => p.IsPublished)
            .OrderByDescending(p => p.Version)
            .Select(p => (int?)p.Version)
            .FirstOrDefaultAsync();

    /// <summary>
    /// The zone if the host recognises it, UTC otherwise.
    ///
    /// Refusing an unknown zone would fail the registration of an app that is
    /// otherwise working perfectly — the only consequence of the fallback is
    /// that a server-pushed fixed-time reminder lands at the wrong local hour
    /// for that one install, which is a far smaller failure than not registering.
    /// </summary>
    private static string ResolveTimeZone(string raw)
    {
        var id = raw.Trim();
        return TimeZoneInfo.TryFindSystemTimeZoneById(id, out _) ? id : "UTC";
    }

    private static string? Blank(string? raw)
    {
        var text = raw?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
