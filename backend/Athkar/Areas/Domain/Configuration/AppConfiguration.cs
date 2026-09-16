using Athkar.Shareds.Enums;
using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Configuration;

/// <summary>
/// Platform-wide settings the admin owns from the CMS, read by every client.
///
/// Exactly one live row exists — the seeder creates it and the service only ever
/// updates it — so there is nothing to pick between when a client asks for "the"
/// configuration.
/// </summary>
public class AppConfiguration : AuditableEntity
{
    // ── Branding ──

    /// <summary>
    /// Brand primary as <c>#RRGGBB</c>. The clients derive every other shade
    /// from it, so this single value re-skins both the app and the CMS.
    /// Defaults to the deep green of the manuscript design direction.
    /// </summary>
    public string PrimaryColor { get; set; } = "#2B6B4A";

    // ── Prayer-time defaults ──
    //
    // Defaults only. The device computes its own times and the reader may
    // override any of this in settings; these are what a fresh install starts
    // on, so that a first launch in Riyadh shows Umm al-Qura without anybody
    // having to go looking for it.

    public CalculationMethod DefaultCalculationMethod { get; set; } = CalculationMethod.UmmAlQura;

    public Madhab DefaultMadhab { get; set; } = Madhab.Standard;

    /// <summary>
    /// Minutes after sunrise that morning adhkar are suggested at, and minutes
    /// after Asr for the evening. Here rather than hardcoded in the app because
    /// local custom differs and an admin should be able to follow it.
    /// </summary>
    public int MorningOffsetMinutes { get; set; } = 30;

    public int EveningOffsetMinutes { get; set; } = 30;

    // ── Content sync ──

    /// <summary>
    /// Bumped by the content services on every publish, unpublish or edit that a
    /// reader would see.
    ///
    /// This is the whole sync protocol: the app sends what it has, and gets
    /// either a delta or, far more often, nothing. Keeping it a single integer
    /// rather than per-entity timestamps means the common case — an app that is
    /// up to date — costs one comparison.
    /// </summary>
    public int ContentVersion { get; set; } = 1;

    /// <summary>
    /// The smallest app version still allowed to talk to this server, as a
    /// build number. Zero means no floor. Exists so a breaking change to the
    /// content shape can be rolled out without silently corrupting old installs.
    /// </summary>
    public int MinimumAppBuild { get; set; }

    // ── About and attribution ──

    /// <summary>
    /// The scholar who reviewed the content, named on the About screen. The
    /// project's central claim rests on this person, so it is a configured value
    /// that an admin must fill in rather than a string in the app's source.
    /// </summary>
    public string? ReviewingScholar { get; set; }

    /// <summary>Their ijaza or credential, one line, shown beneath the name.</summary>
    public string? ReviewingScholarCredential { get; set; }

    // ── Support contact ──
    //
    // Every channel is optional and starts unset: a fresh install has no support
    // desk yet, and the app hides a channel it has no value for rather than
    // offering a dead link. Null and "" mean the same to a client, so the
    // service stores the empty form as null.

    public string? SupportEmail { get; set; }
    public string? SupportWebsite { get; set; }
    public string? PrivacyPolicyUrl { get; set; }

    /// <summary>Store ids, so the app can send a reader to the right review page.</summary>
    public string? AndroidPackageName { get; set; }
    public string? IosAppStoreId { get; set; }
}
