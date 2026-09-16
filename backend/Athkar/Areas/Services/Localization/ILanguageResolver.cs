using Athkar.Shareds.Attributes;

namespace Athkar.Areas.Services.Localization;

/// <summary>
/// Turns whatever a caller asked for into a language code this system actually
/// has content in.
///
/// Its own small service because every reader-facing endpoint needs the same
/// answer and the rule has to be identical in all of them: an enabled language
/// if the caller named one, the default otherwise. A per-service reimplementation
/// is how a catalogue ends up in Arabic while the reminder that pointed at it
/// arrives in English.
/// </summary>
[ScopedInjectable]
public interface ILanguageResolver
{
    /// <summary>The best enabled language for the requested code. Never throws, never returns null.</summary>
    Task<string> Resolve(string? requested);

    /// <summary>The platform default — the end of every fallback chain.</summary>
    Task<string> Default();
}
