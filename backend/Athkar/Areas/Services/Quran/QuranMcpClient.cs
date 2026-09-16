using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models.Config;

namespace Athkar.Areas.Services.Quran;

/// <summary>
/// One block of Qur'an as the canonical source returns it.
/// </summary>
/// <param name="Reference">«2:285-286», echoed back from the request.</param>
/// <param name="Display">The ayat in the Uthmani edition, in order.</param>
/// <param name="Search">The same ayat in the plain edition, in order.</param>
/// <param name="English">The same ayat translated, in order.</param>
public record QuranBlockText(
    string Reference,
    IReadOnlyList<string> Display,
    IReadOnlyList<string> Search,
    IReadOnlyList<string> English);

/// <summary>
/// Talks to the quran.ai MCP server.
///
/// MCP over HTTP is JSON-RPC with two wrinkles worth knowing before reading the
/// implementation: the transport answers in <c>text/event-stream</c> even for a
/// single reply, and the session is stateful — a session id comes back on the
/// initialize response and every later call must carry it.
///
/// The server also asks callers to acknowledge its grounding rules and echo a
/// nonce. That is not ceremony to route around: the rules say, in the server's
/// own words, that a client must never produce Qur'anic text from memory. This
/// service is the shape of agreeing with that.
/// </summary>
[ScopedInjectable]
public interface IQuranMcpClient
{
    /// <summary>Whether the deployment has this source switched on at all.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Fetches every reference in one session. Throws <see cref="QuranMcpException"/>
    /// on any failure — the caller treats a partial read as no read.
    /// </summary>
    Task<IReadOnlyList<QuranBlockText>> FetchBlocks(
        IReadOnlyList<string> references, CancellationToken cancellation = default);
}

/// <summary>A fault talking to the canonical source. Never surfaces as a 500; see the sync service.</summary>
public class QuranMcpException : Exception
{
    public QuranMcpException(string message, Exception? inner = null) : base(message, inner) { }
}

public class QuranMcpClient : IQuranMcpClient
{
    /// <summary>Named client so the timeout and base address live in one place.</summary>
    public const string HttpClientName = "quran-mcp";

    private readonly IHttpClientFactory factory;
    private readonly ILogger<QuranMcpClient> logger;
    private readonly QuranMcpSettings settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public QuranMcpClient(
        IHttpClientFactory factory,
        ILogger<QuranMcpClient> logger,
        IOptions<QuranMcpSettings> options)
    {
        this.factory = factory;
        this.logger = logger;
        settings = options.Value;
    }

    public bool IsEnabled => settings.Enabled && !string.IsNullOrWhiteSpace(settings.BaseUrl);

    public async Task<IReadOnlyList<QuranBlockText>> FetchBlocks(
        IReadOnlyList<string> references, CancellationToken cancellation = default)
    {
        if (!IsEnabled) throw new QuranMcpException("The canonical Qur'an source is disabled.");

        using var http = factory.CreateClient(HttpClientName);
        var session = new Session(http, settings, JsonOptions);

        await session.Initialize(cancellation);
        await session.AcknowledgeGroundingRules(cancellation);

        var blocks = new List<QuranBlockText>(references.Count);

        foreach (var reference in references)
        {
            var display = await session.FetchEdition("fetch_quran", reference, settings.DisplayEdition, cancellation);
            var search = await session.FetchEdition("fetch_quran", reference, settings.SearchEdition, cancellation);
            var english = await session.FetchEdition("fetch_translation", reference, settings.EnglishEdition, cancellation);

            // The editions are read separately and must line up ayah for ayah. If
            // they do not, something upstream changed shape and quietly zipping
            // them would pair an ayah with a neighbour's translation.
            if (display.Count == 0 || display.Count != search.Count || display.Count != english.Count)
                throw new QuranMcpException(
                    $"{reference}: the source returned {display.Count}/{search.Count}/{english.Count} " +
                    "ayat for the display, search and English editions.");

            blocks.Add(new QuranBlockText(reference, display, search, english));
        }

        logger.LogInformation("Read {Count} Qur'anic blocks from {Host}.", blocks.Count, settings.BaseUrl);

        return blocks;
    }

    /// <summary>
    /// One MCP conversation. Short-lived by design: the session id is held for
    /// the duration of a sync and then dropped.
    /// </summary>
    private sealed class Session
    {
        private readonly HttpClient http;
        private readonly QuranMcpSettings settings;
        private readonly JsonSerializerOptions json;

        private string? sessionId;
        private string? groundingNonce;
        private int nextId;

        public Session(HttpClient http, QuranMcpSettings settings, JsonSerializerOptions json)
        {
            this.http = http;
            this.settings = settings;
            this.json = json;
        }

        public async Task Initialize(CancellationToken cancellation)
        {
            await Send(new
            {
                jsonrpc = "2.0",
                id = ++nextId,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new { },
                    clientInfo = new { name = "athkari-api", version = "1" },
                },
            }, cancellation);

            // A notification: no id, and no reply is expected. Skipping it leaves
            // the server waiting and the next call answering with an error.
            await Send(new { jsonrpc = "2.0", method = "notifications/initialized" }, cancellation, expectReply: false);
        }

        /// <summary>
        /// Reads the rules and keeps the nonce. Echoing it is what stops the
        /// server from prepending the full rules text to every later response.
        /// </summary>
        public async Task AcknowledgeGroundingRules(CancellationToken cancellation)
        {
            var text = TextOf(await Call("fetch_grounding_rules", new { }, cancellation));

            const string marker = "GROUNDING_NONCE:";
            var at = text.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0) return;

            groundingNonce = new string(text[(at + marker.Length)..]
                .TrimStart()
                .TakeWhile(c => !char.IsWhiteSpace(c) && c != '<')
                .ToArray());
        }

        public async Task<IReadOnlyList<string>> FetchEdition(
            string tool, string reference, string edition, CancellationToken cancellation)
        {
            var texts = new List<string>();
            object arguments = new
            {
                ayahs = reference,
                editions = new[] { edition },
                grounding_nonce = groundingNonce,
            };

            // A long range comes back truncated with a continuation token and no
            // error of any kind. Ignoring it is how a pull loses آية الكرسي and
            // reports success — see tools/quran-mcp/pull.py, which learned this
            // the hard way.
            while (true)
            {
                var payload = ParsePayload(TextOf(await Call(tool, arguments, cancellation)), reference);

                if (payload.TryGetProperty("results", out var results) &&
                    results.TryGetProperty(edition, out var rows))
                {
                    foreach (var row in rows.EnumerateArray())
                        texts.Add(row.GetProperty("text").GetString() ?? string.Empty);
                }

                var continuation = payload.TryGetProperty("pagination", out var pagination) &&
                                   pagination.ValueKind == JsonValueKind.Object &&
                                   pagination.TryGetProperty("continuation", out var token) &&
                                   token.ValueKind == JsonValueKind.String
                    ? token.GetString()
                    : null;

                if (string.IsNullOrEmpty(continuation)) return texts;

                arguments = new { continuation, grounding_nonce = groundingNonce };
            }
        }

        private async Task<JsonElement> Call(string tool, object arguments, CancellationToken cancellation) =>
            await Send(new
            {
                jsonrpc = "2.0",
                id = ++nextId,
                method = "tools/call",
                @params = new { name = tool, arguments },
            }, cancellation);

        private async Task<JsonElement> Send(object body, CancellationToken cancellation, bool expectReply = true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.BaseUrl)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(body, json), Encoding.UTF8, "application/json"),
            };

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            if (sessionId is not null) request.Headers.TryAddWithoutValidation("mcp-session-id", sessionId);

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, cancellation);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new QuranMcpException($"The canonical Qur'an source at {settings.BaseUrl} could not be reached.", ex);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                    throw new QuranMcpException($"The canonical Qur'an source answered {(int)response.StatusCode}.");

                if (sessionId is null &&
                    response.Headers.TryGetValues("mcp-session-id", out var ids))
                    sessionId = ids.FirstOrDefault();

                var raw = await response.Content.ReadAsStringAsync(cancellation);
                if (!expectReply) return default;

                return ReadResult(raw);
            }
        }

        /// <summary>
        /// Unwraps the reply. The transport is SSE, so the JSON-RPC message
        /// arrives on a <c>data:</c> line rather than as the whole body.
        /// </summary>
        private JsonElement ReadResult(string raw)
        {
            var line = raw
                .Split('\n')
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.StartsWith("data:", StringComparison.Ordinal))
                ?[5..] ?? raw;

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException ex)
            {
                throw new QuranMcpException("The canonical Qur'an source answered with something that was not JSON.", ex);
            }

            var root = document.RootElement.Clone();
            document.Dispose();

            if (root.TryGetProperty("error", out var error))
                throw new QuranMcpException($"The canonical Qur'an source refused the request: {error}");

            return root.TryGetProperty("result", out var result) ? result : root;
        }

        private static string TextOf(JsonElement result)
        {
            if (!result.TryGetProperty("content", out var content)) return string.Empty;

            var builder = new StringBuilder();
            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var type) && type.GetString() == "text")
                    builder.AppendLine(part.GetProperty("text").GetString());
            }

            return builder.ToString();
        }

        /// <summary>
        /// The tools answer with a JSON object on the first line and a plain
        /// English reminder about citation on the lines after it. Only the first
        /// line is the payload.
        /// </summary>
        private static JsonElement ParsePayload(string text, string reference)
        {
            var first = text.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith('{'));
            if (first is null)
                throw new QuranMcpException($"{reference}: the source returned no data.");

            try
            {
                using var document = JsonDocument.Parse(first);
                return document.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                throw new QuranMcpException($"{reference}: the source's reply could not be read.", ex);
            }
        }
    }
}
