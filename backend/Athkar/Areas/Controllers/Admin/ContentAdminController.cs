using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Content;
using Athkar.Areas.Services.Content.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Admin;

/// <summary>
/// Content editing. Open to editors — this is the one admin area whose whole
/// point is that somebody other than an administrator works in it every day.
/// </summary>
[AppAuthorize(Roles.Editor, Roles.Admin, Roles.SuperAdmin)]
[Route("api/v1/admin/content")]
public class ContentAdminController : BaseApiController
{
    private readonly IContentAdminService service;
    private readonly IAdhkarImportService import;
    private readonly ITakhrijSyncService takhrij;

    public ContentAdminController(
        IContentAdminService service,
        IAdhkarImportService import,
        ITakhrijSyncService takhrij)
    {
        this.service = service;
        this.import = import;
        this.takhrij = takhrij;
    }

    // ── categories ──

    [HttpGet("categories")]
    public Task<BaseResponse<PageOutput<AdminCategoryOutput>>> ListCategories([FromQuery] PageInput input) =>
        service.ListCategories(input);

    [HttpGet("categories/{id:int}")]
    public Task<BaseResponse<AdminCategoryOutput>> GetCategory(int id) => service.GetCategory(id);

    [HttpPost("categories")]
    public Task<BaseResponse<AdminCategoryOutput>> CreateCategory([FromBody] CategoryInput input) =>
        service.CreateCategory(input);

    [HttpPut("categories/{id:int}")]
    public Task<BaseResponse<AdminCategoryOutput>> UpdateCategory(int id, [FromBody] CategoryInput input) =>
        service.UpdateCategory(id, input);

    [HttpDelete("categories/{id:int}")]
    public Task<BaseResponse> DeleteCategory(int id) => service.DeleteCategory(id);

    [HttpPost("categories/reorder")]
    public Task<BaseResponse> ReorderCategories([FromBody] ReorderInput input) =>
        service.ReorderCategories(input);

    // ── adhkar ──

    [HttpGet("adhkar")]
    public Task<BaseResponse<PageOutput<AdminDhikrOutput>>> ListAdhkar(
        [FromQuery] int? categoryId, [FromQuery] PageInput input) =>
        service.ListAdhkar(categoryId, input);

    [HttpGet("adhkar/{id:int}")]
    public Task<BaseResponse<AdminDhikrOutput>> GetDhikr(int id) => service.GetDhikr(id);

    [HttpPost("adhkar")]
    public Task<BaseResponse<AdminDhikrOutput>> CreateDhikr([FromBody] DhikrInput input) =>
        service.CreateDhikr(input);

    [HttpPut("adhkar/{id:int}")]
    public Task<BaseResponse<AdminDhikrOutput>> UpdateDhikr(int id, [FromBody] DhikrInput input) =>
        service.UpdateDhikr(id, input);

    [HttpDelete("adhkar/{id:int}")]
    public Task<BaseResponse> DeleteDhikr(int id) => service.DeleteDhikr(id);

    [HttpPost("adhkar/reorder")]
    public Task<BaseResponse> ReorderAdhkar([FromBody] ReorderInput input) =>
        service.ReorderAdhkar(input);

    [HttpPost("adhkar/{id:int}/publish")]
    public Task<BaseResponse<AdminDhikrOutput>> Publish(int id) =>
        service.SetDhikrPublished(id, true);

    [HttpPost("adhkar/{id:int}/unpublish")]
    public Task<BaseResponse<AdminDhikrOutput>> Unpublish(int id) =>
        service.SetDhikrPublished(id, false);

    // ── importing حصن المسلم ──

    /// <summary>
    /// What an import of حصن المسلم's أبواب would add. Writes nothing — creating
    /// a hundred and more chapters is not something to discover after clicking.
    /// </summary>
    [HttpGet("adhkar/import")]
    public Task<BaseResponse<AdhkarImportOutput>> PreviewImport() => import.Preview();

    /// <summary>
    /// Adds the missing أبواب and their adhkar. Every dhikr lands unpublished:
    /// the source carries no takhrij, and this project does not put unattributed
    /// narration in front of a reader.
    ///
    /// Admin rather than Editor. The rest of this controller is open to editors
    /// because their work is one row at a time; this writes hundreds at once.
    /// </summary>
    [HttpPost("adhkar/import")]
    [AppAuthorize(Roles.Admin, Roles.SuperAdmin)]
    public Task<BaseResponse<AdhkarImportOutput>> ApplyImport() => import.Apply();

    // ── attributing those drafts from the book's own footnotes ──

    /// <summary>
    /// What filling the drafts' takhrij would write. Writes nothing.
    /// </summary>
    [HttpGet("adhkar/takhrij")]
    public Task<BaseResponse<TakhrijSyncOutput>> PreviewTakhrij() => takhrij.Preview();

    /// <summary>
    /// Fills the book, the reference and any grading the footnote states, for
    /// every draft حصن المسلم's footnotes account for.
    ///
    /// It publishes nothing. The rows become publishable; whether they are
    /// published stays an editor's decision, one row at a time, which is the
    /// same door all content comes through.
    /// </summary>
    [HttpPost("adhkar/takhrij")]
    [AppAuthorize(Roles.Admin, Roles.SuperAdmin)]
    public Task<BaseResponse<TakhrijSyncOutput>> ApplyTakhrij([FromBody] TakhrijSyncInput? input) =>
        takhrij.Apply(input);
}
