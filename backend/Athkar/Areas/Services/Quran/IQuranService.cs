using Microsoft.AspNetCore.Http;
using Athkar.Areas.Services.Quran.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Quran;

/// <summary>
/// The mushaf, handled as one prepared file rather than as a table of verses.
/// See <c>QuranPackage</c> for why, and <c>docs/BUSINESS_LOGIC.md</c> §7 for the
/// schema the uploaded file is expected to have.
/// </summary>
[ScopedInjectable]
public interface IQuranService
{
    /// <summary>
    /// Whether there is anything newer than <paramref name="knownVersion"/> for
    /// the given edition.
    /// </summary>
    /// <param name="edition">
    /// Null means "whichever is the default" — which is what every install from
    /// before editions existed asks, and it must keep working.
    /// </param>
    Task<BaseResponse<QuranVersionOutput>> CheckVersion(int? knownVersion, string? edition = null);

    /// <summary>
    /// The mushafs a reader may choose between: every published edition, one row
    /// each. Empty when nothing is published, which is not an error.
    /// </summary>
    Task<BaseResponse<List<QuranEditionOutput>>> Editions();

    /// <summary>
    /// Opens the published package of an edition for streaming. Returns null
    /// when that edition has nothing published; the controller turns that into
    /// the error envelope.
    /// </summary>
    Task<(Stream Content, string FileName, string Sha256)?> OpenPublished(string? edition = null);

    /// <summary>Counts a completed download. Called after the stream closes, never before.</summary>
    Task RecordDownload(string? edition = null);

    Task<BaseResponse<List<QuranPackageOutput>>> List();

    Task<BaseResponse<QuranPackageOutput>> Upload(QuranUploadInput input, IFormFile file);

    /// <summary>
    /// Publishes one package and withdraws whichever was published before
    /// <b>of the same edition</b> — one file per mushaf is offered at a time,
    /// and the other mushafs are left alone.
    /// </summary>
    Task<BaseResponse<QuranPackageOutput>> Publish(int id);

    /// <summary>
    /// Makes this package's edition the one a device gets when it names none.
    /// Only a published package can be it: a default nobody can download would
    /// leave every fresh install with no mushaf and no error to show for it.
    /// </summary>
    Task<BaseResponse<QuranPackageOutput>> SetDefault(int id);

    /// <summary>
    /// Withdraws the published package, leaving nothing published.
    ///
    /// The app's version check then reports that there is nothing to download,
    /// which is the honest answer and the one it already handles — an install
    /// that has the file keeps it. This exists because publishing was otherwise
    /// a one-way door: a package could only be replaced by another, so a mushaf
    /// found to have a defect could not be taken down at all, and could never be
    /// deleted either.
    /// </summary>
    Task<BaseResponse<QuranPackageOutput>> Unpublish(int id);

    /// <summary>
    /// Corrects what a package *says about itself* — script, waqf flag, release
    /// notes. Never the bytes, the version or the checksum: an install that has
    /// already downloaded this version would have no way to learn that it
    /// changed.
    /// </summary>
    Task<BaseResponse<QuranPackageOutput>> Edit(int id, QuranPackageEditInput input);

    /// <summary>
    /// Opens any package for the console to download, published or not.
    ///
    /// Distinct from <see cref="OpenPublished"/>, which is the app's endpoint
    /// and offers exactly the one published file. This is how an admin gets back
    /// what was actually stored — to open it, to hash it, to check that the
    /// upload is the file they meant.
    /// </summary>
    Task<(Stream Content, string FileName, string Sha256)?> OpenForAdmin(int id);

    Task<BaseResponse> Delete(int id);
}
