using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Logging;

/// <summary>
/// One row per <c>/api/</c> request, written by the logging middleware.
///
/// Recorded ahead of authentication so rejected calls are captured too: a 401
/// storm is exactly the kind of thing you need the log to have caught, and a
/// logger that runs after auth never sees one.
/// </summary>
public class ApiLog : BaseEntity
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? QueryString { get; set; }

    public int StatusCode { get; set; }

    /// <summary>The envelope's <c>ErrorCode</c>, where the response carried one.</summary>
    public int? ErrorCode { get; set; }

    public long DurationMs { get; set; }

    /// <summary>The acting staff user, read after the pipeline unwinds. Null for device calls.</summary>
    public int? UserId { get; set; }

    /// <summary>The calling device's key, when the request carried one.</summary>
    public string? DeviceKey { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
