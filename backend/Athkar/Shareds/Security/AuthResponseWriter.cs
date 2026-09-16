using System.Text.Json;
using Athkar.Shareds.Models;

namespace Athkar.Shareds.Security;

/// <summary>
/// Writes the envelope for the two rejections the framework would otherwise
/// answer with an empty body — leaving a client with a status code and nothing
/// to show the person holding the phone.
/// </summary>
public static class AuthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteUnauthorizedAsync(HttpContext context) =>
        Write(context, StatusCodes.Status401Unauthorized, ErrorCode.Unauthorized);

    public static Task WriteForbiddenAsync(HttpContext context) =>
        Write(context, StatusCodes.Status403Forbidden, ErrorCode.Forbidden);

    private static async Task Write(HttpContext context, int status, ErrorCode code)
    {
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(BaseResponse.Fail(code), JsonOptions));
    }
}
