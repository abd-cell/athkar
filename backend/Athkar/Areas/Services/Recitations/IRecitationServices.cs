using Athkar.Areas.Services.Content.Models;
using Athkar.Areas.Services.Recitations.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Recitations;

/// <summary>
/// The console's half of the recitation slice. Anything that changes what a
/// reader would see bumps the content version — reciters ride the catalogue.
/// </summary>
[ScopedInjectable]
public interface IRecitationAdminService
{
    Task<BaseResponse<PageOutput<AdminReciterOutput>>> ListReciters(ReciterListInput input);
    Task<BaseResponse<AdminReciterOutput>> GetReciter(int id);
    Task<BaseResponse<AdminReciterOutput>> UpdateReciter(int id, ReciterInput input);
    Task<BaseResponse<AdminReciterOutput>> UpdateRecitation(int reciterId, int recitationId, RecitationInput input);
    Task<BaseResponse> DeleteReciter(int id);
    Task<BaseResponse> ReorderReciters(ReorderInput input);
}

/// <summary>
/// Refreshes the reciter catalogue from its publisher.
///
/// It writes drafts and stops there. Which reciters a waqf app offers, and
/// which of their recordings, is an editor's choice; a sync that published
/// would hand that choice to whoever maintains the publisher's list.
/// </summary>
[ScopedInjectable]
public interface IRecitationSyncService
{
    /// <summary>What a sync would do. Writes nothing.</summary>
    Task<BaseResponse<RecitationSyncOutput>> Preview();

    /// <summary>Writes the reciters an editor chose from the check, as drafts.</summary>
    Task<BaseResponse<RecitationSyncOutput>> Apply(RecitationSyncInput? input = null);
}
