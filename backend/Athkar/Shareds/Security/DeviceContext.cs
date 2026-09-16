using Athkar.Shareds.Models;

namespace Athkar.Shareds.Security;

public class DeviceContext : IDeviceContext
{
    /// <summary>The header every reader-facing call carries.</summary>
    public const string HeaderName = "X-Device-Key";

    private readonly IHttpContextAccessor http;

    public DeviceContext(IHttpContextAccessor http) => this.http = http;

    public string? DeviceKey
    {
        get
        {
            var raw = http.HttpContext?.Request.Headers[HeaderName].FirstOrDefault();
            return IsWellFormed(raw) ? raw!.Trim() : null;
        }
    }

    public string RequireDeviceKey() =>
        DeviceKey ?? throw new AppException(ErrorCode.InvalidDeviceKey);

    /// <summary>
    /// A UUID, in any of the forms the platforms generate. Checked rather than
    /// trusted because the value reaches a database column and an index, and
    /// "whatever the caller sent" is not a length the schema agreed to.
    /// </summary>
    private static bool IsWellFormed(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw.Trim(), out _);
}
