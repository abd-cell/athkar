using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Configuration;
using Athkar.Areas.Services.Configuration.Models;
using Athkar.Areas.Services.Devices;
using Athkar.Areas.Services.Devices.Models;
using Athkar.Areas.Services.Management;
using Athkar.Areas.Services.Management.Models;
using Athkar.Areas.Services.Staff;
using Athkar.Areas.Services.Staff.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Admin;

/// <summary>
/// The console's own surface: dashboard, settings, staff, and the two logs.
/// Grouped in one controller because each is a handful of read-only actions and
/// splitting them would be five files of two methods.
/// </summary>
[AppAuthorize(Roles.Admin, Roles.SuperAdmin)]
[Route("api/v1/admin")]
public class ManagementController : BaseApiController
{
    private readonly IManagementService management;
    private readonly IAppConfigurationService configuration;
    private readonly IStaffService staff;
    private readonly IDeviceService devices;

    public ManagementController(
        IManagementService management,
        IAppConfigurationService configuration,
        IStaffService staff,
        IDeviceService devices)
    {
        this.management = management;
        this.configuration = configuration;
        this.staff = staff;
        this.devices = devices;
    }

    [AppAuthorize(Roles.Editor, Roles.Admin, Roles.SuperAdmin)]
    [HttpGet("dashboard")]
    public Task<BaseResponse<DashboardOutput>> Dashboard() => management.Dashboard();

    [HttpGet("devices/stats")]
    public Task<BaseResponse<DeviceStatsOutput>> DeviceStats() => devices.Stats();

    [HttpGet("configuration")]
    public Task<BaseResponse<AppConfigurationOutput>> GetConfiguration() => configuration.Get();

    [HttpPut("configuration")]
    public Task<BaseResponse<AppConfigurationOutput>> UpdateConfiguration(
        [FromBody] AppConfigurationInput input) =>
        configuration.Update(input);

    [HttpGet("audit")]
    public Task<BaseResponse<PageOutput<AuditOutput>>> Audit(
        [FromQuery] string? action, [FromQuery] PageInput input) =>
        management.Audit(action, input);

    [HttpGet("logs")]
    public Task<BaseResponse<PageOutput<ApiLogOutput>>> Logs(
        [FromQuery] int? statusCode, [FromQuery] PageInput input) =>
        management.ApiLogs(statusCode, input);

    // ── staff: the one area a plain admin may not touch ──

    [AppAuthorize(Roles.SuperAdmin)]
    [HttpGet("staff")]
    public Task<BaseResponse<PageOutput<StaffOutput>>> ListStaff([FromQuery] PageInput input) =>
        staff.List(input);

    [AppAuthorize(Roles.SuperAdmin)]
    [HttpPost("staff")]
    public Task<BaseResponse<StaffOutput>> CreateStaff([FromBody] StaffInput input) =>
        staff.Create(input);

    [AppAuthorize(Roles.SuperAdmin)]
    [HttpPut("staff/{id:int}")]
    public Task<BaseResponse<StaffOutput>> UpdateStaff(int id, [FromBody] StaffInput input) =>
        staff.Update(id, input);

    [AppAuthorize(Roles.SuperAdmin)]
    [HttpDelete("staff/{id:int}")]
    public Task<BaseResponse> DeleteStaff(int id) => staff.Delete(id);
}
