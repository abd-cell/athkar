using System.ComponentModel.DataAnnotations;
using Athkar.Shareds.Enums;

namespace Athkar.Areas.Services.Quran.Models;

/// <summary>
/// What a device asks for before downloading a couple of hundred megabytes:
/// whether there is anything newer than what it has.
/// </summary>
public class QuranVersionOutput
{
    /// <summary>
    /// Which mushaf this answer is about. Echoed back even when the device did
    /// not name one, so an install that asked for "whatever the default is"
    /// learns what it actually got and can store its copy under that name.
    /// </summary>
    public string? Edition { get; set; }

    /// <summary>The reader-facing name of that edition.</summary>
    public string? Name { get; set; }

    /// <summary>Null when nothing is published — a perfectly normal state at launch.</summary>
    public int? Version { get; set; }

    public bool UpdateAvailable { get; set; }

    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public QuranScript Script { get; set; }
    public bool HasWaqfAnnotations { get; set; }
    public string? ReleaseNotes { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public class QuranPackageOutput
{
    public int Id { get; set; }
    public string Edition { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int Version { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public QuranScript Script { get; set; }
    public bool HasWaqfAnnotations { get; set; }
    public string? ReleaseNotes { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int DownloadCount { get; set; }
    public DateTime UploadedAt { get; set; }

    public QuranPackageOutput() { }

    public QuranPackageOutput(Domain.Quran.QuranPackage e)
    {
        Id = e.Id;
        Edition = e.Edition;
        Name = e.Name;
        IsDefault = e.IsDefault;
        Version = e.Version;
        FileName = e.FileName;
        SizeBytes = e.SizeBytes;
        Sha256 = e.Sha256;
        Script = e.Script;
        HasWaqfAnnotations = e.HasWaqfAnnotations;
        ReleaseNotes = e.ReleaseNotes;
        IsPublished = e.IsPublished;
        PublishedAt = e.PublishedAt;
        DownloadCount = e.DownloadCount;
        UploadedAt = e.CreationDate;
    }
}

public class QuranUploadInput
{
    /// <summary>
    /// The mushaf this file is a version of. A slug, lower-case, and the same
    /// one every time that mushaf is re-uploaded — it is what a reader's device
    /// stores its copy under, so a typo here is a second edition rather than a
    /// new version of the first.
    /// </summary>
    [Required, StringLength(64, MinimumLength = 2)]
    [RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$",
        ErrorMessage = "Use lower-case letters, digits and single hyphens.")]
    public string Edition { get; set; } = string.Empty;

    /// <summary>What the reader sees in the list of mushafs.</summary>
    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Version { get; set; }

    [EnumDataType(typeof(QuranScript))]
    public QuranScript Script { get; set; } = QuranScript.Uthmani;

    /// <summary>
    /// Whether the file carries the waqf tables. Declared by the uploader rather
    /// than probed, because opening an arbitrary uploaded SQLite file to read
    /// its schema is a wider door than this feature needs.
    /// </summary>
    public bool HasWaqfAnnotations { get; set; }

    [StringLength(2000)]
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// Optional. When given, the server checks the bytes it received hash to
    /// this and refuses the upload otherwise — so a mushaf truncated on the way
    /// up is caught here rather than on a reader's phone.
    /// </summary>
    [StringLength(64, MinimumLength = 64)]
    public string? ExpectedSha256 { get; set; }
}

/// <summary>
/// The parts of a package an admin may correct after it is uploaded.
///
/// Only the describing fields. The version, the bytes, the size and the
/// checksum are all immutable, and deliberately so: an install that already
/// downloaded version 3 has no way to be told that version 3 now means
/// something else. To change the text you upload a new version.
/// </summary>
public class QuranPackageEditInput
{
    /// <summary>
    /// The reader-facing name. Editable for the same reason the script is: it
    /// describes the file rather than identifying it. The edition slug is not
    /// here, and will not be — it is what installs follow.
    /// </summary>
    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Which script the file is set in. Editable because it describes the file
    /// rather than changing it — an uploader who picked the wrong one from the
    /// list should not have to send 60MB again to fix a label.
    /// </summary>
    [EnumDataType(typeof(QuranScript))]
    public QuranScript Script { get; set; } = QuranScript.Uthmani;

    /// <summary>Whether the file carries the waqf tables. Declared, never probed.</summary>
    public bool HasWaqfAnnotations { get; set; }

    [StringLength(2000)]
    public string? ReleaseNotes { get; set; }
}

/// <summary>
/// One mushaf the reader may choose, as the app sees it.
///
/// Deliberately not <see cref="QuranPackageOutput"/>: that row carries the
/// storage key's neighbours — the uploader, the download count, whether a
/// package is published — which are the console's business. This is the
/// reader's: a name, a script, and what downloading it would cost them.
/// </summary>
public class QuranEditionOutput
{
    public string Edition { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public QuranScript Script { get; set; }

    /// <summary>The published version of this edition — what a device compares its copy against.</summary>
    public int Version { get; set; }

    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public bool HasWaqfAnnotations { get; set; }
    public string? ReleaseNotes { get; set; }

    /// <summary>Which one a device gets when it asks without naming an edition.</summary>
    public bool IsDefault { get; set; }

    public DateTime? PublishedAt { get; set; }

    public QuranEditionOutput() { }

    public QuranEditionOutput(Domain.Quran.QuranPackage e)
    {
        Edition = e.Edition;
        Name = e.Name;
        Script = e.Script;
        Version = e.Version;
        SizeBytes = e.SizeBytes;
        Sha256 = e.Sha256;
        HasWaqfAnnotations = e.HasWaqfAnnotations;
        ReleaseNotes = e.ReleaseNotes;
        IsDefault = e.IsDefault;
        PublishedAt = e.PublishedAt;
    }
}
