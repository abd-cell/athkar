using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Widgets;
using Athkar.Areas.Services.Widgets.Models;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Public;

/// <summary>
/// The widget rules, as the app reads them on launch. Anonymous, like the rest
/// of the reader-facing surface.
/// </summary>
[Route("api/v1/widget")]
public class WidgetController : BaseApiController
{
    private readonly IWidgetService service;

    public WidgetController(IWidgetService service) => this.service = service;

    [HttpGet]
    public Task<BaseResponse<WidgetSettingsOutput>> Get() => service.Get();

    /// <summary>
    /// The gallery the reader browses, plus the rules it is browsed under.
    ///
    /// One call: the app fetches both on every sync and neither half is any use
    /// without the other.
    /// </summary>
    [HttpGet("catalog")]
    public Task<BaseResponse<WidgetCatalogBundleOutput>> Catalog([FromQuery] string? language) =>
        service.Catalog(language);
}
