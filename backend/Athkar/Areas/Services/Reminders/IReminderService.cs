using Athkar.Areas.Services.Reminders.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Reminders;

[ScopedInjectable]
public interface IReminderService
{
    Task<BaseResponse<PageOutput<ReminderOutput>>> List(PageInput input);
    Task<BaseResponse<ReminderOutput>> Get(int id);
    Task<BaseResponse<ReminderOutput>> Create(ReminderInput input);
    Task<BaseResponse<ReminderOutput>> Update(int id, ReminderInput input);
    Task<BaseResponse> Delete(int id);

    /// <summary>
    /// The device-local campaigns this install should schedule itself, worded in
    /// its language. Anonymous; the device key decides nothing but which
    /// language and platform filter applies.
    /// </summary>
    Task<BaseResponse<List<DeviceReminderOutput>>> ForDevice(string? languageCode);
}
