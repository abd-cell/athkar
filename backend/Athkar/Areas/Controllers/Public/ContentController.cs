using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Content;
using Athkar.Areas.Services.Content.Models;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Public;

[Route("api/v1/content")]
public class ContentController : BaseApiController
{
    private readonly IContentService service;

    public ContentController(IContentService service) => this.service = service;

    /// <summary>
    /// The published catalogue. Pass the version you already hold and the usual
    /// answer is "nothing has changed" rather than the corpus again.
    /// </summary>
    [HttpGet("catalog")]
    public Task<BaseResponse<CatalogOutput>> Catalog(
        [FromQuery] string? language, [FromQuery] int? version) =>
        service.Catalog(language, version);

    [HttpGet("search")]
    public Task<BaseResponse<PageOutput<SearchHitOutput>>> Search(
        [FromQuery] string? language, [FromQuery] PageInput input) =>
        service.Search(language, input);
}
