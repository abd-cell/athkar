using Athkar.Shareds.Attributes;

namespace Athkar.Shareds.Files;

/// <summary>
/// Somewhere to put bytes too big for a database column. Paths are opaque,
/// relative keys the caller stores alongside its row; only the storage knows
/// where they land.
///
/// One implementation today (the local disk). It is an interface so a deployment
/// that outgrows a single machine can swap in object storage without the
/// services that save Qur'an packages knowing.
/// </summary>
[SingletonInjectable]
public interface IFileStorage
{
    /// <summary>
    /// Writes <paramref name="content"/> under <paramref name="folder"/> and
    /// returns the storage key. The name is generated — an uploader's own file
    /// name is never part of a path, so it cannot traverse or collide.
    /// </summary>
    Task<string> SaveAsync(string folder, string extension, Stream content, CancellationToken ct = default);

    /// <summary>Opens a stored file for reading, or null when the key no longer resolves.</summary>
    Stream? OpenRead(string key);

    /// <summary>Size in bytes, or null when the key no longer resolves.</summary>
    long? SizeOf(string key);

    Task DeleteAsync(string key);
}
