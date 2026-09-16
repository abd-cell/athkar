using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Reminders;
using Athkar.Areas.Services.Reminders.Models;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Public;

[Route("api/v1/reminders")]
public class RemindersController : BaseApiController
{
    private readonly IReminderService service;

    public RemindersController(IReminderService service) => this.service = service;

    /// <summary>
    /// The campaigns this install schedules itself. Prayer-anchored reminders
    /// are always in here rather than pushed, because only the device knows when
    /// Maghrib is where it is standing.
    /// </summary>
    [HttpGet]
    public Task<BaseResponse<List<DeviceReminderOutput>>> Get([FromQuery] string? language) =>
        service.ForDevice(language);
}
