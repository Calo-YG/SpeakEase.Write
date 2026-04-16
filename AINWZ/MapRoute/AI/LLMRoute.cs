using AINWZ.Application.Contracts.AI.Dto;
using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace AINWZ.MapRoute.AI
{
    public static class LLMRoute
    {
        public static void MapLLMEndPoint(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/llm")
               .WithDescription("LLM 服务")
               .WithTags("llm")
               .RequireAuthorization();
            // 在此处添加 LLM 相关的端点映射，例如：
            // group.MapPost("chat", ...).WithName("chat").RequireAuthorization();

            app.MapGet("/ai/skills", (ILLMSkillRegistry skillRegistry,  IOptions<JsonOptions> options) =>
            {
                return Results.Json(skillRegistry.GetAll(), options.Value.SerializerOptions);
            })
           .WithName("GetLLMSkills")
           .RequireAuthorization();

            app.MapPost("/ai/chat", async (LLMChatRequestDto request, ILLMService llmService, IOptions<JsonOptions> options, CancellationToken cancellationToken) =>
            {
                var jsonOptions = options.Value.SerializerOptions;

                try
                {
                    var response = await llmService.ChatAsync(new LLMChatRequest
                    {
                        Model = request.Model,
                        FallbackModels = request.FallbackModels,
                        SystemPrompt = request.SystemPrompt,
                        Messages = request.Messages,
                        Temperature = request.Temperature,
                        MaxTokens = request.MaxTokens,
                        UseJsonMode = request.UseJsonMode,
                        Tools = request.Tools,
                        ToolChoice = request.ToolChoice,
                        EnableAutoToolDispatch = request.EnableAutoToolDispatch,
                        SkillName = request.SkillName,
                        SkillOverridePrompt = request.SkillOverridePrompt
                    }, cancellationToken);

                    return Results.Json(response, jsonOptions);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    var detailMessage = exception.InnerException?.Message ?? exception.Message;

                    return Results.Json(new
                    {
                        code = "llm_chat_failed",
                        message = "LLM 对话调用失败。",
                        details = detailMessage,
                        requestId = string.Empty,
                    }, jsonOptions, statusCode: StatusCodes.Status502BadGateway);
                }
            })
            .WithName("ChatWithLLM")
            .RequireAuthorization();

            app.MapPost("/ai/chat/stream", async (LLMChatRequestDto request,  IHttpContextAccessor httpContextAccessor, ILLMService llmService,  IOptions<JsonOptions> options, CancellationToken cancellationToken) =>
            {
                var jsonOptions = options.Value.SerializerOptions;

                var httpContext = httpContextAccessor.HttpContext;

                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                httpContext.Response.Headers.ContentType = "text/event-stream";
                httpContext.Response.Headers.CacheControl = "no-cache";

                try
                {
                    await foreach (var streamEvent in llmService.StreamAsync(new LLMChatRequest
                    {
                        Model = request.Model,
                        FallbackModels = request.FallbackModels,
                        SystemPrompt = request.SystemPrompt,
                        Messages = request.Messages,
                        Temperature = request.Temperature,
                        MaxTokens = request.MaxTokens,
                        UseJsonMode = request.UseJsonMode,
                        Tools = request.Tools,
                        ToolChoice = request.ToolChoice,
                        EnableAutoToolDispatch = request.EnableAutoToolDispatch,
                        SkillName = request.SkillName,
                        SkillOverridePrompt = request.SkillOverridePrompt
                    }, cancellationToken))
                    {
                        var eventName = string.IsNullOrWhiteSpace(streamEvent.Type) ? "message" : streamEvent.Type;
                        var eventPayload = JsonSerializer.Serialize(streamEvent, jsonOptions);

                        await httpContext.Response.WriteAsync($"event: {eventName}\n", Encoding.UTF8, cancellationToken);
                        await httpContext.Response.WriteAsync($"data: {eventPayload}\n\n", Encoding.UTF8, cancellationToken);
                        await httpContext.Response.Body.FlushAsync(cancellationToken);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    var errorEvent = new LLMStreamEvent
                    {
                        Type = "error",
                        RequestId = null,
                        ErrorCode = "llm_stream_unhandled",
                        ErrorMessage = exception.Message
                    };

                    var errorPayload = JsonSerializer.Serialize(errorEvent, jsonOptions);
                    await httpContext.Response.WriteAsync("event: error\n", Encoding.UTF8, cancellationToken);
                    await httpContext.Response.WriteAsync($"data: {errorPayload}\n\n", Encoding.UTF8, cancellationToken);
                    await httpContext.Response.Body.FlushAsync(cancellationToken);
                }
            })
            .WithName("StreamChatWithLLM")
            .RequireAuthorization();
        }
    }
}
