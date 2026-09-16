using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Configuration;
using Athkar.Areas.Services.Configuration.Models;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Public;

/// <summary>
/// The first call every client makes. Anonymous by necessity — the app needs the
/// brand colour and the prayer defaults before it has painted anything.
/// </summary>
[Route("api/v1/configuration")]
public class ConfigurationController : BaseApiController
{
    private readonly IAppConfigurationService service;

    public ConfigurationController(IAppConfigurationService service) => this.service = service;

    [HttpGet]
    public Task<BaseResponse<AppConfigurationOutput>> Get() => service.Get();
}
