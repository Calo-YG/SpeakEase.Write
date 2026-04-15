using AINWZ.Application.Contracts.AI.Dto;
using AINWZ.Infrastructure.JsonConverters;
using AINWZ.Infrastructure.LLM;
using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;
using AINWZ.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using System.Text;
using System.Text.Json;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructurePersistence(builder.Configuration);
builder.Services.AddLLM(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(op =>
{
    op.SerializerOptions.Converters.Add(new DateTimeConverter());
    op.SerializerOptions.Converters.Add(new DateTimeNullConverter());
    op.SerializerOptions.Converters.Add(new LongConverter());
    op.SerializerOptions.Converters.Add(new LongNullConverter());
});

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(); // scalar/v1
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/ai/skills", ([FromServices] ILLMSkillRegistry skillRegistry, [FromServices] IOptions<JsonOptions> options) =>
{
    return Results.Json(skillRegistry.GetAll(), options.Value.SerializerOptions);
})
.WithName("GetLLMSkills");

app.MapPost("/ai/chat", async (LLMChatRequestDto request, [FromServices] ILLMService llmService, [FromServices] IOptions<JsonOptions> options, CancellationToken cancellationToken) =>
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
.WithName("ChatWithLLM");

app.MapPost("/ai/chat/stream", async (LLMChatRequestDto request, [FromServices] IHttpContextAccessor httpContextAccessor, [FromServices] ILLMService llmService, [FromServices] IOptions<JsonOptions> options, CancellationToken cancellationToken) =>
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
.WithName("StreamChatWithLLM");

await app.RunAsync();
