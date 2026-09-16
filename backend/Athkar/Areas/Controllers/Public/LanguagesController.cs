using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Localization;
using Athkar.Areas.Services.Localization.Models;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Public;

[Route("api/v1/languages")]
public class LanguagesController : BaseApiController
{
    private readonly ILocalizationService service;

    public LanguagesController(ILocalizationService service) => this.service = service;

    [HttpGet]
    public Task<BaseResponse<List<LanguageOutput>>> Get() => service.EnabledLanguages();

    /// <summary>
    /// The interface-copy overlay for one language. The app merges this over the
    /// strings compiled into the build, so a missing key is not an error.
    /// </summary>
    [HttpGet("{code}/strings")]
    public Task<BaseResponse<UiStringsOutput>> Strings(string code) => service.Strings(code);
}
