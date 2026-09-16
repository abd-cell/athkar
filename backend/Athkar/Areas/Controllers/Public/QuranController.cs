using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Quran;
using Athkar.Areas.Services.Quran.Models;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Public;

[Route("api/v1/quran")]
public class QuranController : BaseApiController
{
    private readonly IQuranService service;

    public QuranController(IQuranService service) => this.service = service;

    [HttpGet("check-version")]
    public Task<BaseResponse<QuranVersionOutput>> CheckVersion(
        [FromQuery] int? version, [FromQuery] string? edition) =>
        service.CheckVersion(version, edition);

    /// <summary>
    /// The mushafs on offer, so the reader can choose one rather than being
    /// given whichever the admin published last.
    ///
    /// Anonymous, like the rest of the reader-facing API: choosing a mushaf is
    /// not something the server is told about. The device downloads what it
    /// picked and keeps the choice locally.
    /// </summary>
    [HttpGet("editions")]
    public Task<BaseResponse<List<QuranEditionOutput>>> Editions() => service.Editions();

    /// <summary>
    /// Streams the published package.
    ///
    /// The one endpoint in this system that does not answer with
    /// <c>BaseResponse</c>: it answers with a couple of hundred megabytes of
    /// file, and wrapping that in an envelope would mean buffering a mushaf in
    /// memory to base64 it. The checksum travels in a header so the app can
    /// verify what it received without a second call.
    /// </summary>
    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] string? edition)
    {
        var package = await service.OpenPublished(edition);

        if (package is null)
            return Ok(BaseResponse.Fail(ErrorCode.NoPublishedQuranPackage));

        var (content, fileName, sha256) = package.Value;

        Response.Headers["X-Content-Sha256"] = sha256;

        // enableRangeProcessing, so an interrupted download resumes rather than
        // starting the whole file again — which on a phone on mobile data is the
        // difference between the feature working and not.
        await service.RecordDownload(edition);
        return File(content, "application/octet-stream", fileName, enableRangeProcessing: true);
    }
}
