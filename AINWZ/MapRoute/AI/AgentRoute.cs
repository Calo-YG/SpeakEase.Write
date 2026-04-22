using SpeakEase.Write.Application.Contracts.AI;
using SpeakEase.Write.Application.Contracts.AI.Dto;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace SpeakEase.Write.MapRoute.AI;

/// <summary>
/// Agent 对话路由
/// </summary>
public static class AgentRoute
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapAgentEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("ai/agent")
           .WithDescription("Agent 对话")
           .WithTags("agent")
           .RequireAuthorization();

        // 非流式 Agent 对话
        group.MapPost("chat", async (AgentChatRequestDto request, IAgentApplication agentApp, CancellationToken cancellationToken) =>
        {
            var response = await agentApp.ChatAsync(request, cancellationToken);
            return Results.Ok(response);
        }).WithName("agent_chat");

        // 流式 SSE Agent 对话
        group.MapPost("chat/stream", async (HttpContext httpContext, AgentChatRequestDto request, IAgentApplication agentApp, CancellationToken cancellationToken) =>
        {
            httpContext.Response.ContentType = "text/event-stream";
            httpContext.Response.Headers.CacheControl = "no-cache";

            await WriteSSEStreamAsync(httpContext.Response.Body, agentApp, request, cancellationToken);
        }).WithName("agent_chat_stream");
    }

    /// <summary>
    /// 将 Agent 流式响应写入 SSE 流
    /// </summary>
    private static async Task WriteSSEStreamAsync(Stream stream, IAgentApplication agentApp, AgentChatRequestDto request, CancellationToken ct)
    {
        await foreach (var chunk in agentApp.StreamChatAsync(request, ct))
        {
            var eventType = chunk.Type switch
            {
                "content" => "content",
                "tool_call" => "tool_call",
                "tool_result" => "tool_result",
                "done" => "done",
                _ => chunk.Type
            };

            var payload = chunk.Type switch
            {
                "content" => JsonSerializer.Serialize(new { type = "content", content = chunk.Content }, JsonOptions),
                "tool_call" => JsonSerializer.Serialize(new { type = "tool_call", toolCallDelta = chunk.ToolCallDelta }, JsonOptions),
                "tool_result" => JsonSerializer.Serialize(new { type = "tool_result", toolResult = chunk.ToolResult }, JsonOptions),
                "done" => JsonSerializer.Serialize(new { type = "done", finalResponse = chunk.FinalResponse }, JsonOptions),
                _ => JsonSerializer.Serialize(new { type = chunk.Type }, JsonOptions)
            };

            var line = $"event: {eventType}\ndata: {payload}\n\n";
            await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(line), ct);
        }
    }
}
