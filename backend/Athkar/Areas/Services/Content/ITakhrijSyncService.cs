using Athkar.Areas.Services.Content.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Content;

/// <summary>
/// Fills the imported drafts' attribution from حصن المسلم's own footnotes.
///
/// It writes the takhrij and stops there. Nothing is published: the rule that
/// keeps unattributed narration off a reader's screen is the same rule that
/// keeps *unread* attribution off it, and a file cannot do an editor's reading
/// for them. What this removes is the typing, not the judgement.
/// </summary>
[ScopedInjectable]
public interface ITakhrijSyncService
{
    /// <summary>What the sync would fill. Writes nothing.</summary>
    Task<BaseResponse<TakhrijSyncOutput>> Preview();

    /// <summary>
    /// Fills the attribution of the rows an editor chose from the check — or of
    /// every row the footnotes account for when the choice is left empty.
    /// </summary>
    Task<BaseResponse<TakhrijSyncOutput>> Apply(TakhrijSyncInput? input = null);
}
