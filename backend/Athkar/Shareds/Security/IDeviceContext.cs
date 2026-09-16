using Athkar.Shareds.Attributes;

namespace Athkar.Shareds.Security;

/// <summary>
/// The calling install, read from the <c>X-Device-Key</c> header.
///
/// This is what stands in for authentication on every reader-facing endpoint.
/// It is deliberately not a security boundary: the key is a UUID the app minted
/// for itself, anyone could send someone else's, and nothing behind it is worth
/// stealing — an inbox of adhkar reminders and a language preference. The
/// endpoints it gates are the ones where writing to *somebody's* row is
/// harmless and writing to *nobody's* is not.
///
/// Anything that could matter if forged — content, settings, broadcasts — sits
/// behind <c>[AppAuthorize]</c> and a staff token instead.
/// </summary>
[ScopedInjectable]
public interface IDeviceContext
{
    /// <summary>The header value, or null when the caller sent none.</summary>
    string? DeviceKey { get; }

    /// <summary>The header value, or throws <c>InvalidDeviceKey</c>.</summary>
    string RequireDeviceKey();
}
