using Athkar.Areas.Services.Staff.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Staff;

[ScopedInjectable]
public interface IStaffService
{
    Task<BaseResponse<PageOutput<StaffOutput>>> List(PageInput input);

    Task<BaseResponse<StaffOutput>> Create(StaffInput input);

    Task<BaseResponse<StaffOutput>> Update(int id, StaffInput input);

    Task<BaseResponse> Delete(int id);
}
