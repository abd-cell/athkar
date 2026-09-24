using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Data;
using Athkar.Areas.Services.Data.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Admin;

/// <summary>
/// Whole-database export and restore, for backup and environment sync. Every
/// table in the model — staff accounts and session tokens included — so this
/// is narrower than the rest of the admin surface: only a superadmin, who
/// already manages staff accounts, can reach it.
/// </summary>
[AppAuthorize(Roles.SuperAdmin)]
[Route("api/v1/admin/data")]
public class DataPortabilityController : BaseApiController
{
    /// <summary>
    /// The transport ceiling for an import upload. A generous headroom over
    /// today's whole-database dump (a few megabytes) — see
    /// <see cref="Athkar.Areas.Controllers.Admin.QuranAdminController"/> for why
    /// this has to be set here rather than read from configuration.
    /// </summary>
    private const long MaxUploadBytes = 512L * 1024 * 1024;

    private readonly IDataPortabilityService service;

    public DataPortabilityController(IDataPortabilityService service) => this.service = service;

    /// <summary>
    /// Downloads every row of every table as one JSON file — the same shape
    /// <see cref="Import"/> reads back.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var bytes = await service.Export();
        var fileName = $"athkar-data-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        return File(bytes, "application/json", fileName);
    }

    /// <summary>Restores from a file <see cref="Export"/> produced, or a hand-edited one shaped like it.</summary>
    [HttpPost("import")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public Task<BaseResponse<DataImportResult>> Import(IFormFile file) => service.Import(file);
}
