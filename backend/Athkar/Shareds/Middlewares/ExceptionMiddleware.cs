using System.Text.Json;
using Athkar.Shareds.Models;

namespace Athkar.Shareds.Middlewares;

/// <summary>
/// Converts a thrown <see cref="AppException"/> into HTTP 200 with a failed
/// <see cref="BaseResponse"/> body; any other exception becomes HTTP 500.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<ExceptionMiddleware> logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            await WriteAsync(context, StatusCodes.Status200OK,
                BaseResponse.Fail(ex.ErrorCode, ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteAsync(context, StatusCodes.Status500InternalServerError,
                BaseResponse.Fail(ErrorCode.UnknownError));
        }
    }

    private static async Task WriteAsync(HttpContext context, int status, BaseResponse body)
    {
        if (context.Response.HasStarted) return;

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = status;
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
