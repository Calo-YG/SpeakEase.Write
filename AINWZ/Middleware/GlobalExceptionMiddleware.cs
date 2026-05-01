using System.Net;
using System.Text.Json;
using SpeakEase.Write.Infrastructure.Exceptions;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessExceptions ex)
        {
            _logger.LogWarning("业务异常: {Message}", ex.Message);
            await WriteErrorResponse(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            await WriteErrorResponse(context, HttpStatusCode.Unauthorized, "未授权访问");
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("请求已取消: {Path}", context.Request.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "未处理异常: {Path} {Method}", context.Request.Path, context.Request.Method);
            var message = context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment()
                ? ex.ToString()
                : "服务器内部错误，请稍后重试";
            await WriteErrorResponse(context, HttpStatusCode.InternalServerError, message);
        }
    }

    private static async Task WriteErrorResponse(HttpContext context, HttpStatusCode statusCode, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = JsonSerializer.Serialize(new ApiResult(message, (int)statusCode));
        await context.Response.WriteAsync(response);
    }
}
