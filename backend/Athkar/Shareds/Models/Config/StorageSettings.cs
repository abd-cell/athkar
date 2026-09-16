namespace Athkar.Shareds.Models.Config;

public class StorageSettings
{
    /// <summary>
    /// Where uploads land, relative to the content root. Deliberately outside
    /// <c>wwwroot</c>: a Qur'an package is served through the versioned download
    /// endpoint so the server can count and gate it, never as static content.
    /// </summary>
    public string Root { get; set; } = "App_Data/uploads";

    /// <summary>Largest Qur'an package the upload endpoint accepts, in megabytes.</summary>
    public int MaxQuranPackageMb { get; set; } = 256;
}
