using Athkar.Areas.Services.Content.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Content;

/// <summary>
/// The reader-facing half of the content slice. Anonymous: no token, no device
/// key required — a catalogue of published adhkar is public by nature.
/// </summary>
[ScopedInjectable]
public interface IContentService
{
    /// <summary>
    /// The published catalogue in one language.
    ///
    /// Pass the version the caller already holds; when it matches, the answer is
    /// an empty <see cref="CatalogOutput.IsUpToDate"/> payload rather than the
    /// corpus again. That comparison is the entire sync protocol.
    /// </summary>
    Task<BaseResponse<CatalogOutput>> Catalog(string? languageCode, int? knownVersion);

    /// <summary>
    /// Search across published adhkar, matching on the folded Arabic — so a
    /// query typed without a single diacritic still finds fully vocalised text.
    /// See <c>ArabicText</c>.
    /// </summary>
    Task<BaseResponse<PageOutput<SearchHitOutput>>> Search(string? languageCode, PageInput input);
}
