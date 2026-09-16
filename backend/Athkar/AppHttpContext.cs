namespace Athkar;

/// <summary>
/// Static access to the current <see cref="HttpContext"/> for the few places
/// that cannot take a dependency (an EF interceptor, a static helper). Prefer
/// injecting <see cref="IHttpContextAccessor"/> everywhere else.
/// </summary>
public static class AppHttpContext
{
    private static IHttpContextAccessor? accessor;

    public static void Configure(IHttpContextAccessor httpContextAccessor) =>
        accessor = httpContextAccessor;

    public static HttpContext? Current => accessor?.HttpContext;
}
