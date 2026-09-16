using System.ComponentModel.DataAnnotations;
using Athkar.Shareds.Enums;

namespace Athkar.Areas.Services.Configuration.Models;

/// <summary>
/// The configuration as every client reads it.
///
/// Deliberately flat and free of ids or audit columns: the app fetches this
/// before it has shown anything, from an anonymous install, so it must carry
/// nothing that an anonymous caller should not see.
/// </summary>
public class AppConfigurationOutput
{
    public string PrimaryColor { get; set; } = string.Empty;

    public CalculationMethod DefaultCalculationMethod { get; set; }
    public Madhab DefaultMadhab { get; set; }
    public int MorningOffsetMinutes { get; set; }
    public int EveningOffsetMinutes { get; set; }

    /// <summary>Compare against what the app last synced to decide whether to fetch the catalogue.</summary>
    public int ContentVersion { get; set; }

    public int MinimumAppBuild { get; set; }

    public string? ReviewingScholar { get; set; }
    public string? ReviewingScholarCredential { get; set; }

    public string? SupportEmail { get; set; }
    public string? SupportWebsite { get; set; }
    public string? PrivacyPolicyUrl { get; set; }
    public string? AndroidPackageName { get; set; }
    public string? IosAppStoreId { get; set; }

    /// <summary>When the settings last changed, so a client can tell whether its cache is current.</summary>
    public DateTime UpdatedAt { get; set; }

    public AppConfigurationOutput() { }

    public AppConfigurationOutput(Domain.Configuration.AppConfiguration e)
    {
        PrimaryColor = e.PrimaryColor;
        DefaultCalculationMethod = e.DefaultCalculationMethod;
        DefaultMadhab = e.DefaultMadhab;
        MorningOffsetMinutes = e.MorningOffsetMinutes;
        EveningOffsetMinutes = e.EveningOffsetMinutes;
        ContentVersion = e.ContentVersion;
        MinimumAppBuild = e.MinimumAppBuild;
        ReviewingScholar = e.ReviewingScholar;
        ReviewingScholarCredential = e.ReviewingScholarCredential;
        SupportEmail = e.SupportEmail;
        SupportWebsite = e.SupportWebsite;
        PrivacyPolicyUrl = e.PrivacyPolicyUrl;
        AndroidPackageName = e.AndroidPackageName;
        IosAppStoreId = e.IosAppStoreId;
        UpdatedAt = e.ModificationDate ?? e.CreationDate;
    }
}

/// <summary>Admin update payload. Every field is replaced — there is no partial save.</summary>
public class AppConfigurationInput
{
    /// <summary>`#RGB` or `#RRGGBB`. Normalised to the six-digit upper-case form on save.</summary>
    [Required, RegularExpression("^#?([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    public string PrimaryColor { get; set; } = "#2B6B4A";

    [EnumDataType(typeof(CalculationMethod))]
    public CalculationMethod DefaultCalculationMethod { get; set; } = CalculationMethod.UmmAlQura;

    [EnumDataType(typeof(Madhab))]
    public Madhab DefaultMadhab { get; set; } = Madhab.Standard;

    [Range(0, 180)]
    public int MorningOffsetMinutes { get; set; } = 30;

    [Range(0, 180)]
    public int EveningOffsetMinutes { get; set; } = 30;

    [Range(0, int.MaxValue)]
    public int MinimumAppBuild { get; set; }

    [StringLength(200)]
    public string? ReviewingScholar { get; set; }

    [StringLength(500)]
    public string? ReviewingScholarCredential { get; set; }

    [EmailAddress, StringLength(256)]
    public string? SupportEmail { get; set; }

    [Url, StringLength(500)]
    public string? SupportWebsite { get; set; }

    [Url, StringLength(500)]
    public string? PrivacyPolicyUrl { get; set; }

    [StringLength(200)]
    public string? AndroidPackageName { get; set; }

    [StringLength(32)]
    public string? IosAppStoreId { get; set; }
}
