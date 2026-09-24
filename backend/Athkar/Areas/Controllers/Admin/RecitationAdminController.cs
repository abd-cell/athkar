using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Content.Models;
using Athkar.Areas.Services.Recitations;
using Athkar.Areas.Services.Recitations.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Admin;

/// <summary>
/// The reciter catalogue. Curating it — which reciters, which of their
/// recordings, in what order — is an editor's errand, like the rest of the
/// content area. Applying a sync is an admin's, because it writes hundreds of
/// rows at once; the takhrij sync draws the same line.
/// </summary>
[AppAuthorize(Roles.Editor, Roles.Admin, Roles.SuperAdmin)]
[Route("api/v1/admin/recitations")]
public class RecitationAdminController : BaseApiController
{
    private readonly IRecitationAdminService service;
    private readonly IRecitationSyncService sync;

    public RecitationAdminController(IRecitationAdminService service, IRecitationSyncService sync)
    {
        this.service = service;
        this.sync = sync;
    }

    [HttpGet("reciters")]
    public Task<BaseResponse<PageOutput<AdminReciterOutput>>> List([FromQuery] ReciterListInput input) =>
        service.ListReciters(input);

    [HttpGet("reciters/{id:int}")]
    public Task<BaseResponse<AdminReciterOutput>> Get(int id) => service.GetReciter(id);

    [HttpPut("reciters/{id:int}")]
    public Task<BaseResponse<AdminReciterOutput>> Update(int id, [FromBody] ReciterInput input) =>
        service.UpdateReciter(id, input);

    [HttpPut("reciters/{reciterId:int}/recordings/{recitationId:int}")]
    public Task<BaseResponse<AdminReciterOutput>> UpdateRecording(
        int reciterId, int recitationId, [FromBody] RecitationInput input) =>
        service.UpdateRecitation(reciterId, recitationId, input);

    [HttpDelete("reciters/{id:int}")]
    public Task<BaseResponse> Delete(int id) => service.DeleteReciter(id);

    [HttpPost("reciters/reorder")]
    public Task<BaseResponse> Reorder([FromBody] ReorderInput input) => service.ReorderReciters(input);

    /// <summary>What a sync from the publisher would write. Writes nothing.</summary>
    [HttpGet("sync")]
    public Task<BaseResponse<RecitationSyncOutput>> PreviewSync() => sync.Preview();

    /// <summary>Writes the chosen reciters as drafts. Publishes nothing.</summary>
    [HttpPost("sync")]
    [AppAuthorize(Roles.Admin, Roles.SuperAdmin)]
    public Task<BaseResponse<RecitationSyncOutput>> ApplySync([FromBody] RecitationSyncInput? input) =>
        sync.Apply(input);
}
