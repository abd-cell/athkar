using System.Diagnostics;
using System.Text.Json;
using Athkar.Areas.Domain.Logging;
using Athkar.DataAccess;
using Athkar.Shareds.Security;

namespace Athkar.Shareds.Middlewares;

/// <summary>
/// One row per <c>/api/</c> request.
///
/// Placed ahead of authentication so rejected calls are captured too — a 401
/// storm is exactly the thing you need the log to have caught, and a logger that
/// runs after auth never sees one. The acting user is therefore read on the way
/// out, once the pipeline has unwound and the claims exist.
/// </summary>
public class ApiLoggerMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<ApiLoggerMiddleware> logger;

    public ApiLoggerMiddleware(RequestDelegate next, ILogger<ApiLoggerMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task Invoke(HttpContext context, DatabaseService db, ISecurityManager securityManager)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        // The envelope's error code is worth having in the log and lives in the
        // body, which has already been written by the time we get here — so it
        // is captured in passing through a buffer rather than re-read.
        var original = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);

            buffer.Position = 0;
            await buffer.CopyToAsync(original);
        }
        finally
        {
            context.Response.Body = original;
            stopwatch.Stop();

            try
            {
                await Record(context, db, securityManager, buffer, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                // A log that cannot be written must not fail the request it was
                // describing.
                logger.LogError(ex, "Failed to write the API log row.");
            }
        }
    }

    private static async Task Record(HttpContext context, DatabaseService db,
        ISecurityManager securityManager, MemoryStream buffer, long elapsed)
    {
        db.ApiLogs.Add(new ApiLog
        {
            Method = context.Request.Method,
            Path = context.Request.Path.Value ?? string.Empty,
            QueryString = context.Request.QueryString.HasValue
                ? context.Request.QueryString.Value
                : null,
            StatusCode = context.Response.StatusCode,
            ErrorCode = ReadErrorCode(buffer),
            DurationMs = elapsed,
            UserId = securityManager.UserId,
            DeviceKey = context.Request.Headers[DeviceContext.HeaderName].FirstOrDefault(),
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.FirstOrDefault(),
        });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// The <c>errorCode</c> from a <c>BaseResponse</c> body, or null for anything
    /// that is not one — a file download, an empty 401, a 500 page.
    /// </summary>
    private static int? ReadErrorCode(MemoryStream buffer)
    {
        // A download is megabytes and is never an envelope; parsing it would
        // cost more than the request.
        if (buffer.Length is 0 or > 64 * 1024) return null;

        try
        {
            buffer.Position = 0;
            using var document = JsonDocument.Parse(buffer);
            return document.RootElement.TryGetProperty("errorCode", out var code) &&
                   code.TryGetInt32(out var value)
                ? value
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            buffer.Position = 0;
        }
    }
}
