using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Widgets;
using Athkar.Areas.Services.Widgets.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Admin;

/// <summary>
/// The widget gallery, as the console edits it.
///
/// Editor rather than Admin on the read and the write: naming a widget in a new
/// language is editorial work, the same as naming a chapter, and routing it
/// through an administrator only means the wording waits. Deleting an entry is
/// not — it withdraws something from every reader's gallery — so that one keeps
/// the higher bar.
/// </summary>
[AppAuthorize(Roles.Editor, Roles.Admin, Roles.SuperAdmin)]
[Route("api/v1/admin/widget/catalog")]
public class WidgetCatalogAdminController : BaseApiController
{
    private readonly IWidgetService service;

    public WidgetCatalogAdminController(IWidgetService service) => this.service = service;

    [HttpGet]
    public Task<BaseResponse<List<AdminWidgetCatalogOutput>>> List() => service.ListCatalog();

    [HttpPost]
    public Task<BaseResponse<AdminWidgetCatalogOutput>> Create([FromBody] WidgetCatalogInput input) =>
        service.CreateCatalogItem(input);

    [HttpPut("{id:int}")]
    public Task<BaseResponse<AdminWidgetCatalogOutput>> Update(
        int id, [FromBody] WidgetCatalogInput input) => service.UpdateCatalogItem(id, input);

    [HttpPut("reorder")]
    public Task<BaseResponse> Reorder([FromBody] WidgetCatalogReorderInput input) =>
        service.ReorderCatalog(input);

    [AppAuthorize(Roles.Admin, Roles.SuperAdmin)]
    [HttpDelete("{id:int}")]
    public Task<BaseResponse> Delete(int id) => service.DeleteCatalogItem(id);
}
