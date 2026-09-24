namespace Athkar.Shareds.Models.Config;

/// <summary>
/// mp3quran.net — the publisher of the recitation catalogue and its audio.
///
/// The server talks to it for exactly one thing: an admin pressing «مزامنة» in
/// the console to refresh the list of reciters. Readers' phones fetch the audio
/// (and the ayah timings) from the publisher directly; nothing a reader does
/// makes this server call out.
///
/// Off by default, like <see cref="QuranMcpSettings"/>: reaching out to a third
/// party on an admin's click is a decision a deployment makes, not one the
/// binary makes for it. With this false the sync answers
/// <c>RecitationSourceDisabled</c> and the catalogue the console already holds
/// is untouched — reciters already published keep playing.
/// </summary>
public class Mp3QuranSettings
{
    public bool Enabled { get; set; }

    /// <summary>The v3 API root, no trailing slash.</summary>
    public string BaseUrl { get; set; } = "https://mp3quran.net/api/v3";

    /// <summary>
    /// The publisher's name and page, stored on every synced recording. Readers
    /// meet the credit in the FAQ (and in the lock-screen media metadata)
    /// rather than on the player — the least any use of someone else's
    /// recordings owes them, and what tells a reader whose servers they are
    /// listening from. Whether the
    /// publisher's terms permit this particular use is a question for the
    /// deployment to settle with the publisher; nothing in the code can answer
    /// it. Changing this is a deployment's statement about where its audio
    /// comes from — so it is configuration, not a constant.
    /// </summary>
    public string SourceName { get; set; } = "MP3Quran";

    public string SourceUrl { get; set; } = "https://mp3quran.net";

    public int TimeoutSeconds { get; set; } = 30;
}
