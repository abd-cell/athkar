namespace Athkar.Shareds.Models.Config;

/// <summary>
/// Whether and how <c>/swagger</c> is shown.
///
/// This used to be a single <c>app.Environment.IsDevelopment()</c> check in
/// <c>Program.cs</c> — no deployment could turn it on without recompiling, and
/// none could turn it off without one either. Reading it from configuration
/// instead means a deployment decides, the way it already decides push and the
/// Qur'an sync: <see cref="Enabled"/> defaults to off in
/// <c>appsettings.json</c> and <c>appsettings.Development.json</c> switches it
/// on for a local clone, but either file can say otherwise without a code
/// change.
/// </summary>
public class SwaggerSettings
{
    public bool Enabled { get; set; }

    public string Title { get; set; } = "Athkar";

    /// <summary>
    /// The Swagger document's own version string (<c>OpenApiInfo.Version</c>),
    /// not the "v1" document name — that name is also the literal segment in
    /// <see cref="Path"/> below, and the two only agree by convention, not by
    /// reference, so keep them in step by hand if either ever moves.
    /// </summary>
    public string Version { get; set; } = "v1";

    /// <summary>
    /// Where the UI fetches the generated document from. The default matches
    /// <c>UseSwagger()</c>'s own default route template; a deployment behind a
    /// path-prefixing reverse proxy is the reason this is configurable rather
    /// than hardcoded — the internal route stays the same, only what the
    /// browser is told to fetch changes.
    /// </summary>
    public string Path { get; set; } = "/swagger/v1/swagger.json";
}
