using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Audit;
using Athkar.Areas.Domain.Content;
using Athkar.Areas.Domain.Devices;
using Athkar.Areas.Domain.Localization;
using Athkar.Areas.Domain.Logging;
using Athkar.Areas.Domain.Reminders;
using Athkar.Areas.Domain.Support;
using Athkar.Areas.Services.Management.Models;
using Athkar.DataAccess.Repositories;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Extensions;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Management;

public class ManagementService : IManagementService
{
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromDays(30);

    private readonly IRepository<Device> devices;
    private readonly IRepository<AthkarCategory> categories;
    private readonly IRepository<Dhikr> adhkar;
    private readonly IRepository<AppLanguage> languages;
    private readonly IRepository<ReminderCampaign> reminders;
    private readonly IRepository<Feedback> feedback;
    private readonly IRepository<PushDispatch> dispatches;
    private readonly IRepository<AuditLog> auditLogs;
    private readonly IRepository<ApiLog> apiLogs;

    public ManagementService(
        IRepository<Device> devices,
        IRepository<AthkarCategory> categories,
        IRepository<Dhikr> adhkar,
        IRepository<AppLanguage> languages,
        IRepository<ReminderCampaign> reminders,
        IRepository<Feedback> feedback,
        IRepository<PushDispatch> dispatches,
        IRepository<AuditLog> auditLogs,
        IRepository<ApiLog> apiLogs)
    {
        this.devices = devices;
        this.categories = categories;
        this.adhkar = adhkar;
        this.languages = languages;
        this.reminders = reminders;
        this.feedback = feedback;
        this.dispatches = dispatches;
        this.auditLogs = auditLogs;
        this.apiLogs = apiLogs;
    }

    public async Task<BaseResponse<DashboardOutput>> Dashboard()
    {
        var now = DateTime.UtcNow;
        var activeSince = now - ActiveWindow;
        var dayAgo = now.AddDays(-1);
        var monthAgo = now.AddDays(-30).Date;

        var installsByDay = await devices.Query()
            .Where(d => d.CreationDate >= monthAgo)
            .GroupBy(d => d.CreationDate.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync();

        var dashboard = new DashboardOutput
        {
            TotalDevices = await devices.CountAsync(),
            ActiveDevices = await devices.CountAsync(d => d.LastSeenAt >= activeSince),
            ReachableDevices = await devices.CountAsync(d => d.NotificationsEnabled && d.PushToken != null),

            PublishedCategories = await categories.CountAsync(c => c.IsPublished),
            PublishedAdhkar = await adhkar.CountAsync(d => d.IsPublished),
            UnsourcedDrafts = await adhkar.CountAsync(d =>
                !d.IsPublished && (d.SourceBook == null || d.SourceReference == null)),

            EnabledLanguages = await languages.CountAsync(l => l.IsEnabled),
            ActiveReminders = await reminders.CountAsync(r => r.IsEnabled),
            OpenFeedback = await feedback.CountAsync(f =>
                f.Status == FeedbackStatus.New || f.Status == FeedbackStatus.InProgress),

            PushLastDay = await dispatches.Query()
                .Where(d => d.SentAtUtc >= dayAgo)
                .GroupBy(d => d.Status)
                .Select(g => new { Key = g.Key.ToString(), Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count),

            DevicesByPlatform = await devices.Query()
                .GroupBy(d => d.Platform)
                .Select(g => new { Key = g.Key.ToString(), Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count),

            DevicesByLanguage = await devices.Query()
                .GroupBy(d => d.LanguageCode)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count),
        };

        // Gaps filled in here rather than left to the chart: a line that skips
        // the days nobody installed draws a slope where there was a flat.
        var counts = installsByDay.ToDictionary(x => DateOnly.FromDateTime(x.Day), x => x.Count);
        for (var day = DateOnly.FromDateTime(monthAgo); day <= DateOnly.FromDateTime(now); day = day.AddDays(1))
            dashboard.Installs.Add(new DailyCount(day, counts.GetValueOrDefault(day)));

        return new BaseResponse<DashboardOutput>(dashboard);
    }

    public async Task<BaseResponse<PageOutput<AuditOutput>>> Audit(string? action, PageInput input)
    {
        var query = auditLogs.Query();

        if (!string.IsNullOrWhiteSpace(action))
        {
            // A prefix, so "content." narrows to the whole content slice without
            // the caller having to know every action name in it.
            var prefix = action.Trim();
            query = query.Where(a => a.Action.StartsWith(prefix));
        }

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var term = input.Search.Trim();
            query = query.Where(a => a.EntityName.Contains(term) || a.Action.Contains(term));
        }

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(a => a.CreationDate)
            .Paginate(input)
            .Select(a => new AuditOutput
            {
                Id = a.Id,
                Action = a.Action,
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                UserName = a.User!.FullName,
                OldValue = a.OldValue,
                NewValue = a.NewValue,
                CreatedAt = a.CreationDate,
            })
            .ToListAsync();

        return new BaseResponse<PageOutput<AuditOutput>>(new PageOutput<AuditOutput>
        {
            TotalRows = total,
            Data = rows,
        });
    }

    public async Task<BaseResponse<PageOutput<ApiLogOutput>>> ApiLogs(int? statusCode, PageInput input)
    {
        var query = apiLogs.Query();

        if (statusCode is not null) query = query.Where(l => l.StatusCode == statusCode);

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var term = input.Search.Trim();
            query = query.Where(l => l.Path.Contains(term));
        }

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(l => l.CreationDate)
            .Paginate(input)
            .Select(l => new ApiLogOutput
            {
                Id = l.Id,
                Method = l.Method,
                Path = l.Path,
                StatusCode = l.StatusCode,
                ErrorCode = l.ErrorCode,
                DurationMs = l.DurationMs,
                DeviceKey = l.DeviceKey,
                UserId = l.UserId,
                CreatedAt = l.CreationDate,
            })
            .ToListAsync();

        return new BaseResponse<PageOutput<ApiLogOutput>>(new PageOutput<ApiLogOutput>
        {
            TotalRows = total,
            Data = rows,
        });
    }
}
