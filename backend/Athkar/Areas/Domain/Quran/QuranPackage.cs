using Athkar.Shareds.Enums;
using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Quran;

/// <summary>
/// A complete mushaf, uploaded as one prepared file.
///
/// The text is deliberately <b>not</b> decomposed into rows here. A verse table
/// on this server would be a second, worse copy of a corpus that already exists
/// in verified, checksummed form — and every read would have to re-assemble a
/// page the app is perfectly able to query itself. So the CMS uploads a prepared
/// SQLite database, the server stores it whole with a version and a checksum,
/// and the app downloads it once and owns it offline. See
/// <c>docs/BUSINESS_LOGIC.md</c> §7, which also documents the schema that file
/// is expected to contain — including the waqf-mark tables.
/// </summary>
public class QuranPackage : AuditableEntity
{
    /// <summary>
    /// Which mushaf this file is, as a stable slug — <c>hafs-uthmani</c>,
    /// <c>warsh</c>, <c>indopak</c>.
    /// <para>
    /// This is the identity a reader chooses by and a device stores its copy
    /// under, so it is deliberately not the row id: re-uploading Warsh as a new
    /// version must land on the same edition an install is already following,
    /// and an id would change.
    /// </para>
    /// </summary>
    public string Edition { get; set; } = string.Empty;

    /// <summary>
    /// What the reader sees in the list — «مصحف المدينة · حفص». Editable, since
    /// it names the mushaf rather than identifying it; <see cref="Edition"/> is
    /// the identity and never changes.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Monotonic version number the app compares against what it has stored.
    /// Unique <b>within an edition</b> and immutable: a published package is
    /// never edited in place, because an install that already downloaded it
    /// would have no way to know. Two editions are free to both be at version 1
    /// — they are different files followed by different devices.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// The edition handed to a device that asks for the mushaf without naming
    /// one — which is every install from before editions existed, and every
    /// fresh install that has not chosen. Exactly one published package carries
    /// it; publishing another as default clears it here.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>Storage key from <c>IFileStorage</c>. Opaque; not a URL.</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>The uploader's file name, kept for the record and for the download's Content-Disposition.</summary>
    public string FileName { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>
    /// SHA-256, lower-case hex. Computed by the server as the bytes are written,
    /// and verified by the app after download — a mushaf truncated by a dropped
    /// connection must fail loudly rather than open with surahs missing.
    /// </summary>
    public string Sha256 { get; set; } = string.Empty;

    public QuranScript Script { get; set; } = QuranScript.Uthmani;

    /// <summary>
    /// Whether the file carries the tables that make waqf marks tappable — the
    /// advanced option in <c>docs/BUSINESS_LOGIC.md</c> §7.3. The app reads this
    /// to decide whether to offer the explanation sheet at all, rather than
    /// probing the database and guessing.
    /// </summary>
    public bool HasWaqfAnnotations { get; set; }

    /// <summary>What changed, shown to the reader before a re-download.</summary>
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// Only a published package is offered for download. Uploading and
    /// publishing are separate steps so a large file can be transferred and
    /// verified before a million installs are told about it.
    /// </summary>
    public bool IsPublished { get; set; }

    public DateTime? PublishedAt { get; set; }

    /// <summary>Completed downloads. The only usage figure this system keeps.</summary>
    public int DownloadCount { get; set; }
}
