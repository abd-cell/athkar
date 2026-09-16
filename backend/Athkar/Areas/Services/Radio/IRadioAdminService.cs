using Athkar.Areas.Services.Content.Models;
using Athkar.Areas.Services.Radio.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Radio;

/// <summary>
/// The console's half of the radio slice. Every method that changes what a
/// reader would see bumps the content version — stations ride the catalogue, so
/// an edit that does not bump it is an edit that never arrives.
/// </summary>
[ScopedInjectable]
public interface IRadioAdminService
{
    Task<BaseResponse<PageOutput<AdminRadioStationOutput>>> ListStations(PageInput input);
    Task<BaseResponse<AdminRadioStationOutput>> GetStation(int id);
    Task<BaseResponse<AdminRadioStationOutput>> CreateStation(RadioStationInput input);
    Task<BaseResponse<AdminRadioStationOutput>> UpdateStation(int id, RadioStationInput input);
    Task<BaseResponse> DeleteStation(int id);
    Task<BaseResponse> ReorderStations(ReorderInput input);
}
