using Athkar.Areas.Services.Management.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Management;

[ScopedInjectable]
public interface IManagementService
{
    Task<BaseResponse<DashboardOutput>> Dashboard();

    Task<BaseResponse<PageOutput<AuditOutput>>> Audit(string? action, PageInput input);

    Task<BaseResponse<PageOutput<ApiLogOutput>>> ApiLogs(int? statusCode, PageInput input);
}
