using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Quran;
using Athkar.Areas.Services.Quran.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Admin;

[AppAuthorize(Roles.Admin, Roles.SuperAdmin)]
[Route("api/v1/admin/quran")]
public class QuranAdminController : BaseApiController
{
    /// <summary>
    /// The transport ceiling for a package upload.
    ///
    /// Deliberately larger than <c>Storage:MaxQuranPackageMb</c>, which is the
    /// policy and lives in configuration: the service refuses an oversized file
    /// with <see cref="ErrorCode.FileTooLarge"/>, which an admin can read. A
    /// transport limit set to the same number would refuse it first, as an
    /// unexplained form-reading failure. Attribute arguments must be constants,
    /// so this cannot come from configuration.
    /// </summary>
    private const long MaxUploadBytes = 512L * 1024 * 1024;

    private readonly IQuranService service;
    private readonly IQuranAthkarSyncService sync;

    public QuranAdminController(IQuranService service, IQuranAthkarSyncService sync)
    {
        this.service = service;
        this.sync = sync;
    }

    [HttpGet]
    public Task<BaseResponse<List<QuranPackageOutput>>> List() => service.List();

    /// <summary>
    /// Uploads a prepared mushaf file. Multipart, and the only endpoint in the
    /// system that takes one — see <c>QuranPackage</c> for why the text is a file
    /// and not a table.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes)]
    // RequestSizeLimit alone is not enough. The multipart reader has its own
    // ceiling — 128 MB by default — and it is the one that bites first: a
    // package over it fails while reading the form, before any of this runs, as
    // a ValidationError that says nothing about size. That is not a corner
    // case. The letter-perfect QCF mushaf is ~160 MB (docs/BUSINESS_LOGIC.md
    // §7.4), so the default silently refused the very file this endpoint exists
    // to take, while advertising half a gigabyte.
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public Task<BaseResponse<QuranPackageOutput>> Upload(
        [FromForm] QuranUploadInput input, IFormFile file) =>
        service.Upload(input, file);

    [HttpPost("{id:int}/publish")]
    public Task<BaseResponse<QuranPackageOutput>> Publish(int id) => service.Publish(id);

    /// <summary>
    /// Makes this package's edition the one a device gets when it names none —
    /// every install from before editions existed, and every fresh install
    /// before the reader chooses.
    /// </summary>
    [HttpPost("{id:int}/default")]
    public Task<BaseResponse<QuranPackageOutput>> SetDefault(int id) => service.SetDefault(id);

    /// <summary>
    /// Withdraws the published package. Nothing is published afterwards, which
    /// the app's version check reports plainly; an install that already has the
    /// file keeps it.
    /// </summary>
    [HttpPost("{id:int}/unpublish")]
    public Task<BaseResponse<QuranPackageOutput>> Unpublish(int id) => service.Unpublish(id);

    /// <summary>
    /// Corrects what a package says about itself. The bytes, the version and the
    /// checksum are not editable — an install that has this version would have
    /// no way to learn it had changed.
    /// </summary>
    [HttpPut("{id:int}")]
    public Task<BaseResponse<QuranPackageOutput>> Edit(int id, [FromBody] QuranPackageEditInput input) =>
        service.Edit(id, input);

    /// <summary>
    /// Downloads what is actually stored, published or not.
    ///
    /// The second endpoint in the system that streams a file rather than
    /// returning the envelope — the app's own download is the other, and
    /// <c>docs/BUSINESS_LOGIC.md</c> §7.1 explains why bytes cannot be wrapped
    /// in JSON. An admin needs this to hash the stored file and satisfy
    /// themselves it is the one they meant to upload.
    /// </summary>
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var file = await service.OpenForAdmin(id);
        if (file is null) return NotFound();

        // The checksum travels in a header so a caller can verify without
        // re-reading the whole stream twice.
        Response.Headers["X-Checksum-Sha256"] = file.Value.Sha256;

        return File(file.Value.Content, "application/octet-stream", file.Value.FileName);
    }

    [HttpDelete("{id:int}")]
    public Task<BaseResponse> Delete(int id) => service.Delete(id);

    /// <summary>
    /// What a sync against the canonical Qur'an source would change. Reads the
    /// source, writes nothing — the CMS shows this before offering the button
    /// that applies it, because rewriting narrated text is not something an
    /// admin should discover after the fact.
    /// </summary>
    [HttpGet("athkar-sync")]
    public Task<BaseResponse<QuranSyncOutput>> PreviewAthkarSync() => sync.Preview();

    /// <summary>
    /// Re-reads the Qur'anic adhkar from the canonical source and applies the
    /// differences. Never deletes, never touches a row an editor has
    /// re-attributed, and bumps the content version only if something moved.
    /// </summary>
    [HttpPost("athkar-sync")]
    public Task<BaseResponse<QuranSyncOutput>> ApplyAthkarSync() => sync.Apply();
}
