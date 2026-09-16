using System.Text.Json;
using System.Text.Json.Serialization;

namespace Athkar.Areas.Services.Quran;

/// <summary>
/// The list of adhkar that are Qur'an, and the locators that name them.
///
/// Shared by the seeder — which plants them on a fresh database — and the admin
/// sync, which re-reads them from the canonical source. One list, because the
/// two disagreeing would mean a sync that silently ignores a dhikr the seeder
/// planted, and nobody would see it happen.
///
/// The file is produced by <c>tools/quran-mcp/pull.py</c>. Adding a block means
/// adding a reference there and re-running it, not editing text here.
/// </summary>
public static class QuranicAthkarCatalog
{
    /// <summary>Slug of the chapter these are filed under. Deep links and reminders use it.</summary>
    public const string CategoryKey = "quran";

    /// <summary>The book, as written in every <c>Dhikr.SourceBook</c> the sync owns.</summary>
    public const string SourceBook = "القرآن الكريم";

    private const string ArabicIndicDigits = "٠١٢٣٤٥٦٧٨٩";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Lazy<IReadOnlyList<QuranicBlock>> Loaded = new(Load);

    /// <summary>Empty when the data files are absent — every caller treats that as "nothing to do".</summary>
    public static IReadOnlyList<QuranicBlock> Blocks => Loaded.Value;

    /// <summary>
    /// «البقرة: ٢٥٥» for one ayah, «البقرة: ٢٨٥-٢٨٦» for a run.
    ///
    /// This string is not only for display: it is how the sync finds the row a
    /// block belongs to, since <c>Dhikr</c> has no column for a canonical key and
    /// does not need one. An editor who rewrites a reference by hand takes that
    /// row out of the sync's reach, which is the right outcome — it is no longer
    /// the row the source was asked about.
    /// </summary>
    public static string Locator(QuranicBlock block)
    {
        var parts = block.Ayahs[0].Ayah.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var surah)) return block.Reference;

        var name = SurahNames.TryGetValue(surah, out var found) ? found : $"سورة {surah}";
        var from = parts[1];
        var to = block.Ayahs[^1].Ayah.Split(':').ElementAtOrDefault(1) ?? from;

        return from == to
            ? $"{name}: {ToArabicIndic(from)}"
            : $"{name}: {ToArabicIndic(from)}-{ToArabicIndic(to)}";
    }

    private static string ToArabicIndic(string digits) =>
        string.Concat(digits.Select(c => char.IsAsciiDigit(c) ? ArabicIndicDigits[c - '0'] : c));

    private static Dictionary<int, string> SurahNames { get; set; } = [];

    private static IReadOnlyList<QuranicBlock> Load()
    {
        var surahs = Read<List<SurahMeta>>("surahs.json") ?? [];
        SurahNames = surahs
            .GroupBy(s => s.Number)
            .ToDictionary(g => g.Key, g => g.First().NameArabic);

        return Read<List<QuranicBlock>>("athkar_quranic.json") ?? [];
    }

    private static T? Read<T>(string fileName) where T : class
    {
        var path = Path.Combine(AppContext.BaseDirectory, "DataAccess", "Seeders", "Data", fileName);
        if (!File.Exists(path)) return null;

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException)
        {
            // Callers log; a malformed file must not take the process down at startup.
            return null;
        }
    }

    // ── The shapes tools/quran-mcp/pull.py writes ──

    public sealed class QuranicBlock
    {
        [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;
        [JsonPropertyName("name_ar")] public string NameArabic { get; set; } = string.Empty;

        /// <summary>«2:285-286» — the machine-readable form, which is what the MCP tools take.</summary>
        [JsonPropertyName("reference")] public string Reference { get; set; } = string.Empty;

        [JsonPropertyName("ayahs")] public List<QuranicAyah> Ayahs { get; set; } = [];
    }

    public sealed class QuranicAyah
    {
        [JsonPropertyName("ayah")] public string Ayah { get; set; } = string.Empty;

        /// <summary>Uthmani orthography — the text as it is read.</summary>
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;

        /// <summary>The plain edition, folded into <c>Dhikr.SearchText</c>. See the seeder for why.</summary>
        [JsonPropertyName("search_text")] public string? SearchText { get; set; }

        [JsonPropertyName("en")] public string? En { get; set; }
    }

    private sealed class SurahMeta
    {
        [JsonPropertyName("number")] public int Number { get; set; }
        [JsonPropertyName("name_arabic")] public string NameArabic { get; set; } = string.Empty;
    }
}
