using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Devices;
using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Devices.Models;
using Athkar.DataAccess.Repositories;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Extensions;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Devices;

public class DeviceAdminService : IDeviceAdminService
{
    /// <summary>The dashboard's and the push manager's definition of an active install.</summary>
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromDays(30);

    /// <summary>
    /// How much of a push token the list shows. Enough to tell two apart and to
    /// match a row against the Firebase console; far too little to send with.
    /// </summary>
    private const int TokenTailLength = 8;

    private readonly IRepository<Device> devices;
    private readonly IRepository<DeviceNotification> inbox;
    private readonly IAuditService auditService;

    public DeviceAdminService(
        IRepository<Device> devices,
        IRepository<DeviceNotification> inbox,
        IAuditService auditService)
    {
        this.devices = devices;
        this.inbox = inbox;
        this.auditService = auditService;
    }

    public async Task<BaseResponse<PageOutput<DeviceAdminOutput>>> List(DeviceQueryInput input)
    {
        var activeSince = DateTime.UtcNow - ActiveWindow;
        var query = devices.Query();

        if (input.Id is { } id) query = query.Where(d => d.Id == id);
        if (input.Platform is { } platform) query = query.Where(d => d.Platform == platform);

        if (!string.IsNullOrWhiteSpace(input.LanguageCode))
        {
            var code = input.LanguageCode.Trim().ToLowerInvariant();
            query = query.Where(d => d.LanguageCode == code);
        }

        if (input.HasPushToken is { } hasToken)
        {
            query = hasToken
                ? query.Where(d => d.PushToken != null && d.PushToken != "")
                : query.Where(d => d.PushToken == null || d.PushToken == "");
        }

        if (input.NotificationsEnabled is { } enabled)
            query = query.Where(d => d.NotificationsEnabled == enabled);

        if (input.IsActive is { } active)
        {
            query = active
                ? query.Where(d => d.LastSeenAt >= activeSince)
                : query.Where(d => d.LastSeenAt < activeSince);
        }

        // The key is the only text on the row, and it is a UUID — so search is
        // a prefix match on it rather than a free-text query over nothing. An
        // admin pastes a key from a bug report; they never type one.
        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var term = input.Search.Trim();
            query = query.Where(d => d.DeviceKey.Contains(term));
        }

        var total = await query.CountAsync();

        var rows = await query
            .OrderByDescending(d => d.LastSeenAt)
            .ThenByDescending(d => d.Id)
            .Paginate(input)
            .Select(d => new DeviceAdminOutput
            {
                Id = d.Id,
                DeviceKey = d.DeviceKey,
                Platform = d.Platform,
                LanguageCode = d.LanguageCode,
                TimeZoneId = d.TimeZoneId,
                CountryCode = d.CountryCode,
                AppVersion = d.AppVersion,
                HasPushToken = d.PushToken != null && d.PushToken != "",
                // The tail is cut in the database, so the whole token is never
                // read into the web process for a list of a hundred rows — and
                // cannot be logged or serialised by accident.
                PushTokenTail = d.PushToken == null || d.PushToken.Length < TokenTailLength
                    ? null
                    : d.PushToken.Substring(d.PushToken.Length - TokenTailLength),
                NotificationsEnabled = d.NotificationsEnabled,
                LastSeenAt = d.LastSeenAt,
                FirstSeenAt = d.CreationDate,
                SyncedContentVersion = d.SyncedContentVersion,
            })
            .ToListAsync();

        foreach (var row in rows) row.Reach = Reach(row, activeSince);

        await Unread(rows);

        return new BaseResponse<PageOutput<DeviceAdminOutput>>(new PageOutput<DeviceAdminOutput>
        {
            TotalRows = total,
            Data = rows,
        });
    }

    public async Task<BaseResponse<DeviceAdminOutput>> Get(int id)
    {
        var page = await List(new DeviceQueryInput { PageSize = 1, Id = id });
        var row = page.Data?.Data.FirstOrDefault();

        return row is null
            ? BaseResponse<DeviceAdminOutput>.Fail(ErrorCode.DeviceNotFound)
            : new BaseResponse<DeviceAdminOutput>(row);
    }

    public async Task<BaseResponse<PageOutput<DeviceInboxOutput>>> Inbox(int id, PageInput input)
    {
        if (!await devices.AnyAsync(d => d.Id == id))
            return BaseResponse<PageOutput<DeviceInboxOutput>>.Fail(ErrorCode.DeviceNotFound);

        var query = inbox.Query().Where(n => n.DeviceId == id);

        var total = await query.CountAsync();

        var rows = await query
            .OrderByDescending(n => n.CreationDate)
            .ThenByDescending(n => n.Id)
            .Paginate(input)
            .Select(n => new DeviceInboxOutput
            {
                Id = n.Id,
                Kind = n.Kind,
                Title = n.Title,
                Body = n.Body,
                LanguageCode = n.LanguageCode,
                Route = n.Route,
                CreatedAt = n.CreationDate,
                ReadAt = n.ReadAt,
            })
            .ToListAsync();

        return new BaseResponse<PageOutput<DeviceInboxOutput>>(new PageOutput<DeviceInboxOutput>
        {
            TotalRows = total,
            Data = rows,
        });
    }

    public async Task<BaseResponse<string>> RevealPushToken(int id)
    {
        var device = await devices.GetByIdAsync(id);
        if (device is null) return BaseResponse<string>.Fail(ErrorCode.DeviceNotFound);

        if (string.IsNullOrWhiteSpace(device.PushToken))
            return BaseResponse<string>.Fail(ErrorCode.NotFound, "This install holds no token.");

        // Logged before it is returned, and logged whether or not the CMS then
        // shows it: the event worth recording is that somebody asked.
        await auditService.LogAsync(AuditActions.DevicePushTokenReveal, nameof(Device), device.Id);

        return new BaseResponse<string>(device.PushToken);
    }

    /// <summary>
    /// The sender's own ladder, in the sender's order, so a row cannot be in two
    /// states at once and the four counts on the manager keep summing to the
    /// total.
    /// </summary>
    private static DeviceReach Reach(DeviceAdminOutput row, DateTime activeSince) =>
        !row.HasPushToken ? DeviceReach.Tokenless
        : !row.NotificationsEnabled ? DeviceReach.Muted
        : row.LastSeenAt < activeSince ? DeviceReach.Silent
        : DeviceReach.Reachable;

    /// <summary>
    /// Unread counts for the whole page in one grouped query.
    ///
    /// Per-row would be a count per install, and this screen is opened with a
    /// hundred of them.
    /// </summary>
    private async Task Unread(List<DeviceAdminOutput> rows)
    {
        if (rows.Count == 0) return;

        var ids = rows.Select(r => r.Id).ToList();

        var counts = await inbox.Query()
            .Where(n => ids.Contains(n.DeviceId) && n.ReadAt == null)
            .GroupBy(n => n.DeviceId)
            .Select(g => new { DeviceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.DeviceId, g => g.Count);

        foreach (var row in rows)
            row.UnreadCount = counts.TryGetValue(row.Id, out var count) ? count : 0;
    }
}
