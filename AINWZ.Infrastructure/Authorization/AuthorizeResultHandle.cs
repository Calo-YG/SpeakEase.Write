using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Authorization.Authorization;

public class AuthorizeResultHandle(ILoggerFactory factory, IOptions<JsonOptions> options) : IAuthorizationMiddlewareResultHandler
{
    private readonly ILogger _logger = factory.CreateLogger<IAuthorizationMiddlewareResultHandler>();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        //返回鉴权失败信息
        if (authorizeResult.Challenged)
        {
            context!.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            var response = ApiResult.Fail("Authentication failed, token invalid", 401);
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, options.Value.SerializerOptions));

            return;
        }

        //返回授权失败信息
        if (authorizeResult.Forbidden)
        {
            var reason = string.Join(",", authorizeResult.AuthorizationFailure!.FailureReasons.Select(p => p.Message));

            _logger.LogWarning($"Authorization failed  with reason: {reason}");

            context!.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            var response = ApiResult.Fail( reason, 403);
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, options.Value.SerializerOptions));
            return;
        }

        await next(context);
    }
}