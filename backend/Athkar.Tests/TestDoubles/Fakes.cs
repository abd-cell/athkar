using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Configuration;
using Athkar.Areas.Services.Configuration.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Enums;
using Athkar.Areas.Services.Notifications.Models;
using Athkar.Shareds.Models;
using Athkar.Shareds.Models.Base;
using Athkar.Shareds.Security;

namespace Athkar.Tests.TestDoubles;

/// <summary>
/// A unit of work with nothing behind it.
///
/// The in-memory repositories mutate their lists as soon as they are called, so
/// there is nothing left for a save to do — which also means these tests cannot
/// catch a missing `SaveAsync`. That is a known limit of the approach and the
/// reason [SaveCount] is recorded: a test that cares can assert on it.
/// </summary>
public class FakeUnitOfWork : IUnitOfWork
{
    private readonly Dictionary<Type, object> repositories = [];

    public int SaveCount { get; private set; }
    public bool InTransaction { get; private set; }
    public bool RolledBack { get; private set; }

    /// <summary>Registers a repository so the service under test and the test itself share one.</summary>
    public FakeUnitOfWork With<T>(IRepository<T> repository) where T : BaseEntity
    {
        repositories[typeof(T)] = repository;
        return this;
    }

    public IRepository<T> Repository<T>() where T : BaseEntity
    {
        if (repositories.TryGetValue(typeof(T), out var existing)) return (IRepository<T>)existing;

        var created = new InMemoryRepository<T>();
        repositories[typeof(T)] = created;
        return created;
    }

    public Task<int> SaveAsync()
    {
        SaveCount++;
        return Task.FromResult(0);
    }

    public Task BeginTransactionAsync()
    {
        InTransaction = true;
        return Task.CompletedTask;
    }

    public Task CommitAsync()
    {
        SaveCount++;
        InTransaction = false;
        return Task.CompletedTask;
    }

    public Task RollBackAsync()
    {
        RolledBack = true;
        InTransaction = false;
        return Task.CompletedTask;
    }

    public void Detach() { }
}

/// <summary>Records what was audited, so a test can assert the trail is written.</summary>
public class FakeAuditService : IAuditService
{
    public List<string> Actions { get; } = [];

    public Task LogAsync(string action, string entityName, int? entityId,
        object? oldValue = null, object? newValue = null)
    {
        Actions.Add(action);
        return Task.CompletedTask;
    }
}

/// <summary>A signed-in member of staff, with whichever roles the test wants.</summary>
public class FakeSecurityManager : ISecurityManager
{
    public FakeSecurityManager(int? userId = 1, params Roles[] roles)
    {
        UserId = userId;
        Roles = roles.Length == 0 ? [Shareds.Enums.Roles.SuperAdmin] : roles;
    }

    public int? UserId { get; }
    public IReadOnlyCollection<Roles> Roles { get; }

    /// <summary>Settable: the sessions screen has to mark the row making the request.</summary>
    public string? SessionKey { get; set; } = "test-session";

    public int RequireUserId() => UserId ?? throw new AppException(ErrorCode.Unauthorized);

    public bool IsInRole(Roles role) => Roles.Contains(role);

    public bool IsAtLeast(Roles role) => Roles.Any(held => held >= role);
}

/// <summary>
/// Counts content-version bumps.
///
/// Its own fake rather than the real service, because "did this edit reach the
/// phones?" is the single easiest thing to forget in the content slice, and it
/// deserves to be assertable on its own.
/// </summary>
public class FakeAppConfigurationService : IAppConfigurationService
{
    public int Version { get; private set; } = 1;
    public int BumpCount { get; private set; }

    public Task<BaseResponse<AppConfigurationOutput>> Get() =>
        Task.FromResult(new BaseResponse<AppConfigurationOutput>(new AppConfigurationOutput()));

    public Task<BaseResponse<AppConfigurationOutput>> Update(AppConfigurationInput input) =>
        Task.FromResult(new BaseResponse<AppConfigurationOutput>(new AppConfigurationOutput()));

    public Task<int> BumpContentVersion()
    {
        BumpCount++;
        return Task.FromResult(++Version);
    }
}

/// <summary>A language table with Arabic as the default, which is the shipped shape.</summary>
public class FakeLanguageResolver : Areas.Services.Localization.ILanguageResolver
{
    public FakeLanguageResolver(params string[] enabled) =>
        Enabled = enabled.Length == 0 ? ["ar", "en"] : enabled;

    public IReadOnlyList<string> Enabled { get; }

    public Task<string> Resolve(string? requested)
    {
        var code = (requested ?? string.Empty).Split('-')[0].ToLowerInvariant();
        return Task.FromResult(Enabled.Contains(code) ? code : Enabled[0]);
    }

    public Task<string> Default() => Task.FromResult(Enabled[0]);
}

/// <summary>
/// The <c>X-Device-Key</c> header, as a value a test can set.
/// </summary>
public class FakeDeviceContext : Athkar.Shareds.Security.IDeviceContext
{
    public string? DeviceKey { get; set; }

    public string RequireDeviceKey() =>
        DeviceKey ?? throw new AppException(ErrorCode.InvalidDeviceKey);
}

/// <summary>
/// Firebase, as a scriptable queue.
///
/// The point of the double is that the dispatcher's interesting behaviour is
/// entirely in how it *reacts* to what FCM says — a retired token retires the
/// device row, a transient failure is retried until the cap — and none of that
/// can be provoked against the real service on demand.
/// </summary>
public class FakeFcmSender : Athkar.Shareds.Notifications.Fcm.IFcmSender
{
    private readonly Queue<Athkar.Shareds.Notifications.Fcm.FcmSendResult> scripted = new();

    public bool IsConfigured { get; set; } = true;

    /// <summary>Every message handed to the sender, in order.</summary>
    public List<Athkar.Shareds.Notifications.Fcm.FcmMessage> Sent { get; } = [];

    /// <summary>What to answer next. Anything unscripted succeeds.</summary>
    public FakeFcmSender Script(params Athkar.Shareds.Notifications.Fcm.FcmSendResult[] results)
    {
        foreach (var result in results) scripted.Enqueue(result);
        return this;
    }

    public Task<IReadOnlyList<Athkar.Shareds.Notifications.Fcm.FcmSendResult>> SendAsync(
        IReadOnlyList<Athkar.Shareds.Notifications.Fcm.FcmMessage> messages,
        CancellationToken ct = default)
    {
        Sent.AddRange(messages);

        IReadOnlyList<Athkar.Shareds.Notifications.Fcm.FcmSendResult> results =
        [
            .. messages.Select(_ => scripted.Count > 0
                ? scripted.Dequeue()
                : Athkar.Shareds.Notifications.Fcm.FcmSendResult.Sent("fake-message-id")),
        ];

        return Task.FromResult(results);
    }
}

/// <summary>
/// The broadcast service, as a recorder.
///
/// The push manager's send is supposed to be a *delegation* — compose, hand it
/// over, let one code path carry every message — so what is worth asserting is
/// exactly what it passed on and in what order, not what came back.
/// </summary>
public class FakeBroadcastService : Athkar.Areas.Services.Notifications.IBroadcastService
{
    private int nextId = 1;

    /// <summary>Every input handed to Create, in order.</summary>
    public List<BroadcastInput> Created { get; } = [];

    /// <summary>Every id handed to Send, in order.</summary>
    public List<int> SentIds { get; } = [];

    /// <summary>Set to make Create fail, so the caller's error path can be tested.</summary>
    public ErrorCode? CreateFails { get; set; }

    /// <summary>Set to make Send fail after a successful Create.</summary>
    public ErrorCode? SendFails { get; set; }

    public Task<BaseResponse<BroadcastOutput>> Create(BroadcastInput input)
    {
        if (CreateFails is { } code)
            return Task.FromResult(BaseResponse<BroadcastOutput>.Fail(code));

        Created.Add(input);

        return Task.FromResult(new BaseResponse<BroadcastOutput>(new BroadcastOutput
        {
            Id = nextId++,
            Status = BroadcastStatus.Draft,
            Audience = input.Audience,
            TargetDeviceKey = input.TargetDeviceKey,
            ScheduledAtUtc = input.ScheduledAtUtc,
        }));
    }

    public Task<BaseResponse<BroadcastOutput>> Send(int id)
    {
        if (SendFails is { } code)
            return Task.FromResult(BaseResponse<BroadcastOutput>.Fail(code));

        SentIds.Add(id);

        return Task.FromResult(new BaseResponse<BroadcastOutput>(new BroadcastOutput
        {
            Id = id,
            Status = BroadcastStatus.Scheduled,
        }));
    }

    public Task<BaseResponse<PageOutput<BroadcastOutput>>> List(PageInput input) =>
        Task.FromResult(new BaseResponse<PageOutput<BroadcastOutput>>(new PageOutput<BroadcastOutput>()));

    public Task<BaseResponse<BroadcastOutput>> Get(int id) =>
        Task.FromResult(new BaseResponse<BroadcastOutput>(new BroadcastOutput { Id = id }));

    public Task<BaseResponse<BroadcastOutput>> Update(int id, BroadcastInput input) =>
        Task.FromResult(new BaseResponse<BroadcastOutput>(new BroadcastOutput { Id = id }));

    public Task<BaseResponse<BroadcastOutput>> Cancel(int id) =>
        Task.FromResult(new BaseResponse<BroadcastOutput>(new BroadcastOutput { Id = id }));

    public Task<BaseResponse> Delete(int id) => Task.FromResult(new BaseResponse());
}

/// <summary>
/// The dispatcher, counted rather than run.
///
/// The pipeline itself has its own tests; what the manager's "run now" needs to
/// prove is narrower and entirely about orchestration — that all three passes
/// run, in the workers' order, so a reminder that materialises in the pass is
/// also sent by it rather than waiting another minute.
/// </summary>
public class FakePushDispatcher : Athkar.Areas.Services.Notifications.IPushDispatcher
{
    /// <summary>Which passes ran, in the order they were called.</summary>
    public List<string> Calls { get; } = [];

    public int MaterialiseReturns { get; set; }
    public int StartBroadcastsReturns { get; set; }
    public int SendDueReturns { get; set; }

    public Task<int> MaterialiseReminders(CancellationToken ct = default)
    {
        Calls.Add(nameof(MaterialiseReminders));
        return Task.FromResult(MaterialiseReturns);
    }

    public Task<int> SendDue(CancellationToken ct = default)
    {
        Calls.Add(nameof(SendDue));
        return Task.FromResult(SendDueReturns);
    }

    public Task<int> StartDueBroadcasts(CancellationToken ct = default)
    {
        Calls.Add(nameof(StartDueBroadcasts));
        return Task.FromResult(StartBroadcastsReturns);
    }
}

/// <summary>
/// The canonical Qur'an source, without the network.
///
/// Records what was asked for, so a test can assert that the sync read every
/// block and read them once. <see cref="Fault"/> is how the "the source is down"
/// path is exercised — the one that must never become a 500.
/// </summary>
public class FakeQuranMcpClient : Athkar.Areas.Services.Quran.IQuranMcpClient
{
    private readonly Dictionary<string, Athkar.Areas.Services.Quran.QuranBlockText> blocks = [];

    public bool IsEnabled { get; set; } = true;

    /// <summary>When set, every fetch throws it instead of answering.</summary>
    public Athkar.Areas.Services.Quran.QuranMcpException? Fault { get; set; }

    public List<string> Requested { get; } = [];

    public FakeQuranMcpClient With(string reference, string display, string search, string english)
    {
        blocks[reference] = new Athkar.Areas.Services.Quran.QuranBlockText(
            reference, [display], [search], [english]);
        return this;
    }

    public Task<IReadOnlyList<Athkar.Areas.Services.Quran.QuranBlockText>> FetchBlocks(
        IReadOnlyList<string> references, CancellationToken cancellation = default)
    {
        if (Fault is not null) throw Fault;

        Requested.AddRange(references);

        IReadOnlyList<Athkar.Areas.Services.Quran.QuranBlockText> found =
            references.Where(blocks.ContainsKey).Select(r => blocks[r]).ToList();

        return Task.FromResult(found);
    }
}
