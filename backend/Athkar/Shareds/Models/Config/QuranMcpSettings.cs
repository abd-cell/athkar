namespace Athkar.Shareds.Models.Config;

/// <summary>
/// The quran.ai MCP server — the canonical source for Qur'anic text.
///
/// This is the only outbound call the API makes other than FCM, and it is worth
/// being deliberate about why it exists at all. The rule this whole project turns
/// on is that a dhikr on screen traces to a source; for the adhkar that are
/// Qur'an, "the source" is a mushaf, and a mushaf is not something an editor
/// should be retyping into a form. So an admin can ask the server to re-read
/// those verses from the place they actually come from.
///
/// Like <c>FcmSettings</c>, absence is a supported configuration: with
/// <see cref="Enabled"/> false — or no configuration section at all — the sync
/// endpoint reports that the source is switched off and nothing else changes.
/// A development machine with no outbound network still runs.
/// </summary>
public class QuranMcpSettings
{
    /// <summary>
    /// Off by default. Reaching out to a third party on an admin's click is a
    /// decision a deployment makes, not one the binary makes for it.
    /// </summary>
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://mcp.quran.ai";

    /// <summary>
    /// The edition whose text is stored as <c>Dhikr.ArabicText</c>. Note that
    /// "ar-uthmani" is <b>not</b> the Uthmani script — it comes back
    /// undiacritised — which is why the default names the minimal variant.
    /// </summary>
    public string DisplayEdition { get; set; } = "ar-uthmani-minimal";

    /// <summary>
    /// The plain edition folded into <c>Dhikr.SearchText</c>. It spells out the
    /// alif that the Uthmani rasm writes as a dagger mark, and folding *that* is
    /// what a reader's typing matches. See <c>QuranicAthkarSeeder</c>.
    /// </summary>
    public string SearchEdition { get; set; } = "ar-uthmani";

    public string EnglishEdition { get; set; } = "en-sahih-international";

    /// <summary>
    /// Per-request ceiling. The sync makes one call per block, so an unreachable
    /// host should fail the whole operation in seconds rather than hold an
    /// admin's browser open while each one times out in turn.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 20;
}
