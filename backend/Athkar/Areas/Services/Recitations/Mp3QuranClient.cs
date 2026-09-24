using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models.Config;

namespace Athkar.Areas.Services.Recitations;

/// <summary>One reciter as the publisher lists him, in both languages the console keeps.</summary>
public record Mp3QuranReciter(int Id, string NameAr, string? NameEn, IReadOnlyList<Mp3QuranMoshaf> Moshafs);

/// <summary>
/// One recording. <paramref name="TimingReadId"/> is filled by the client from
/// the publisher's timing index, not by the reciters list — the two are
/// separate endpoints joined on the folder URL.
/// </summary>
public record Mp3QuranMoshaf(
    int Id,
    string NameAr,
    string? NameEn,
    string Server,
    string SurahList,
    int SurahTotal,
    int? TimingReadId);

/// <summary>
/// Reads the reciter catalogue from mp3quran.net.
///
/// Three calls, all read-only: the reciters in Arabic, the same list in
/// English (the publisher translates names server-side, keyed on the same
/// ids), and the index of recordings that have ayah timing. The last one is
/// joined on the folder URL — verified exact for every one of the publisher's
/// timed recordings — because timing is a property of one recording, and a
/// reciter-level flag would be wrong for every reciter with more than one.
/// </summary>
[ScopedInjectable]
public interface IMp3QuranClient
{
    bool IsEnabled { get; }

    /// <summary>The whole catalogue, or <see cref="Mp3QuranException"/>. Never a partial list.</summary>
    Task<IReadOnlyList<Mp3QuranReciter>> FetchCatalog(CancellationToken cancellation = default);
}

public class Mp3QuranException : Exception
{
    public Mp3QuranException(string message, Exception? inner = null) : base(message, inner) { }
}

public class Mp3QuranClient : IMp3QuranClient
{
    public const string HttpClientName = "mp3quran";

    private readonly IHttpClientFactory factory;
    private readonly ILogger<Mp3QuranClient> logger;
    private readonly Mp3QuranSettings settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public Mp3QuranClient(
        IHttpClientFactory factory,
        ILogger<Mp3QuranClient> logger,
        IOptions<Mp3QuranSettings> options)
    {
        this.factory = factory;
        this.logger = logger;
        settings = options.Value;
    }

    public bool IsEnabled => settings.Enabled && !string.IsNullOrWhiteSpace(settings.BaseUrl);

    public async Task<IReadOnlyList<Mp3QuranReciter>> FetchCatalog(CancellationToken cancellation = default)
    {
        if (!IsEnabled) throw new Mp3QuranException("The recitation source is disabled.");

        var root = settings.BaseUrl.TrimEnd('/');

        var arabic = await Get<RecitersEnvelope>($"{root}/reciters?language=ar", cancellation);
        var english = await Get<RecitersEnvelope>($"{root}/reciters?language=eng", cancellation);
        var timing = await Get<List<TimingRead>>($"{root}/ayat_timing/reads", cancellation);

        if (arabic?.Reciters is not { Count: > 0 } reciters)
            throw new Mp3QuranException("The publisher returned an empty reciter list.");

        var englishById = (english?.Reciters ?? []).ToDictionary(r => r.Id);
        var timingByFolder = (timing ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t.FolderUrl))
            .GroupBy(t => Folder(t.FolderUrl!))
            .ToDictionary(g => g.Key, g => g.First().Id);

        return
        [
            .. reciters
                .Where(r => r.Id > 0 && !string.IsNullOrWhiteSpace(r.Name))
                .Select(r =>
                {
                    englishById.TryGetValue(r.Id, out var en);
                    var enMoshafs = (en?.Moshaf ?? []).ToDictionary(m => m.Id);

                    return new Mp3QuranReciter(
                        r.Id,
                        r.Name!.Trim(),
                        en?.Name?.Trim(),
                        [
                            .. (r.Moshaf ?? [])
                                .Where(m => m.Id > 0 && !string.IsNullOrWhiteSpace(m.Server))
                                .Select(m => new Mp3QuranMoshaf(
                                    m.Id,
                                    Tidy(m.Name) ?? $"#{m.Id}",
                                    enMoshafs.TryGetValue(m.Id, out var em) ? Tidy(em.Name) : null,
                                    Folder(m.Server!),
                                    (m.SurahList ?? string.Empty).Replace(" ", string.Empty),
                                    m.SurahTotal,
                                    timingByFolder.TryGetValue(Folder(m.Server!), out var read) ? read : null)),
                        ]);
                }),
        ];
    }

    private async Task<T?> Get<T>(string url, CancellationToken cancellation)
    {
        try
        {
            var client = factory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(url, cancellation);

            if (!response.IsSuccessStatusCode)
                throw new Mp3QuranException($"{url} answered {(int)response.StatusCode}.");

            await using var body = await response.Content.ReadAsStreamAsync(cancellation);
            return await JsonSerializer.DeserializeAsync<T>(body, JsonOptions, cancellation);
        }
        catch (Mp3QuranException)
        {
            throw;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(error, "The recitation publisher could not be read at {Url}", url);
            throw new Mp3QuranException($"Could not read {url}.", error);
        }
    }

    /// <summary>
    /// The publisher writes «المصحف المجود - المصحف المجود» when a recording's
    /// type and name coincide. Said once is enough.
    /// </summary>
    public static string? Tidy(string? name)
    {
        var text = name?.Trim();
        if (string.IsNullOrEmpty(text)) return null;

        var halves = text.Split(" - ", 2, StringSplitOptions.TrimEntries);
        return halves.Length == 2 && halves[0] == halves[1] ? halves[0] : text;
    }

    /// <summary>A folder URL, normalised so the reciters list and the timing index compare equal.</summary>
    public static string Folder(string url)
    {
        var text = url.Trim();
        return text.EndsWith('/') ? text : text + "/";
    }

    private sealed class RecitersEnvelope
    {
        public List<ReciterRow>? Reciters { get; set; }
    }

    private sealed class ReciterRow
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public List<MoshafRow>? Moshaf { get; set; }
    }

    private sealed class MoshafRow
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Server { get; set; }

        [JsonPropertyName("surah_list")]
        public string? SurahList { get; set; }

        [JsonPropertyName("surah_total")]
        public int SurahTotal { get; set; }
    }

    private sealed class TimingRead
    {
        public int Id { get; set; }

        [JsonPropertyName("folder_url")]
        public string? FolderUrl { get; set; }
    }
}
