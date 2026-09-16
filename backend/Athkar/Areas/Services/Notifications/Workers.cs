using Athkar.Shareds.Constants;
using Athkar.Shareds.Hosting;

namespace Athkar.Areas.Services.Notifications;

/// <summary>
/// Writes the dispatch rows for reminders coming due.
///
/// Runs at well under the horizon it materialises, so a missed pass is caught by
/// the next one rather than by a reader noticing the silence.
/// </summary>
public class ReminderMaterialiserWorker : PeriodicWorker
{
    public ReminderMaterialiserWorker(
        IServiceScopeFactory scopeFactory, ILogger<ReminderMaterialiserWorker> logger)
        : base(scopeFactory, logger) { }

    protected override TimeSpan Interval => TimeSpan.FromMinutes(PushRules.DispatchHorizonMinutes / 3.0);

    protected override string WorkDescription => "Reminder dispatches materialised";

    protected override Task<int> RunAsync(IServiceProvider services, CancellationToken stoppingToken) =>
        services.GetRequiredService<IPushDispatcher>().MaterialiseReminders(stoppingToken);
}

/// <summary>
/// Sends what is due.
///
/// A minute is the granularity a reader can perceive in a reminder, and the
/// cheapest pass in the system when there is nothing to do: one indexed query
/// that returns no rows.
/// </summary>
public class PushSenderWorker : PeriodicWorker
{
    public PushSenderWorker(IServiceScopeFactory scopeFactory, ILogger<PushSenderWorker> logger)
        : base(scopeFactory, logger) { }

    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

    protected override string WorkDescription => "Push dispatches attempted";

    protected override Task<int> RunAsync(IServiceProvider services, CancellationToken stoppingToken) =>
        services.GetRequiredService<IPushDispatcher>().SendDue(stoppingToken);
}

/// <summary>Turns scheduled broadcasts into dispatch rows.</summary>
public class BroadcastWorker : PeriodicWorker
{
    public BroadcastWorker(IServiceScopeFactory scopeFactory, ILogger<BroadcastWorker> logger)
        : base(scopeFactory, logger) { }

    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

    protected override string WorkDescription => "Broadcasts started";

    protected override Task<int> RunAsync(IServiceProvider services, CancellationToken stoppingToken) =>
        services.GetRequiredService<IPushDispatcher>().StartDueBroadcasts(stoppingToken);
}
