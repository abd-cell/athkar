namespace Athkar.Shareds.Models.Config;

public class FcmSettings
{
    public bool Enabled { get; set; }

    /// <summary>Path to the Firebase service-account JSON, relative to the content root.</summary>
    public string? CredentialsPath { get; set; }

    /// <summary>The same JSON inline, for deployments that inject secrets as environment variables.</summary>
    public string? CredentialsJson { get; set; }

    /// <summary>
    /// How many devices one FCM call carries. Firebase caps a multicast at 500,
    /// and going under it is the difference between one slow device costing a
    /// batch and costing a campaign.
    /// </summary>
    public int BatchSize { get; set; } = 500;
}
