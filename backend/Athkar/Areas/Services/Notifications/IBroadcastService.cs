using Athkar.Areas.Services.Notifications.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Notifications;

[ScopedInjectable]
public interface IBroadcastService
{
    Task<BaseResponse<PageOutput<BroadcastOutput>>> List(PageInput input);
    Task<BaseResponse<BroadcastOutput>> Get(int id);

    /// <summary>Saves a draft. Nothing leaves until <see cref="Send"/> is called.</summary>
    Task<BaseResponse<BroadcastOutput>> Create(BroadcastInput input);

    /// <summary>Edits a draft or a scheduled message. A sent one is immutable.</summary>
    Task<BaseResponse<BroadcastOutput>> Update(int id, BroadcastInput input);

    /// <summary>
    /// Queues it. Sending is always via the worker, even for "send now", so one
    /// code path carries every message and a long fan-out never blocks a request.
    /// </summary>
    Task<BaseResponse<BroadcastOutput>> Send(int id);

    /// <summary>Withdraws a scheduled message that has not started going out.</summary>
    Task<BaseResponse<BroadcastOutput>> Cancel(int id);

    Task<BaseResponse> Delete(int id);
}
