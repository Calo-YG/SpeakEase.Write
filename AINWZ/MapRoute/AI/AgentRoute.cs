using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Application.Contracts.AI;
using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.MapRoute.AI;

public static class AgentRoute
{
    public static void MapAgentEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("ai/agent")
            .WithDescription("AI Agent 服务")
            .WithTags("agent")
            .RequireAuthorization();

        // 非流式 Agent 对话
        group.MapPost("chat", async (
            AgentChatRequestDto request,
            IAgentApplication agentApp,
            CancellationToken ct) =>
        {
            var result = await agentApp.ChatAsync(request, ct);
            return Results.Ok(result);
        }).WithName("agent_chat");

        // 流式 Agent 对话（SSE）
        group.MapPost("chat/stream", async (
            HttpContext httpContext,
            AgentChatRequestDto request,
            IAgentApplication agentApp,
            CancellationToken ct) =>
        {
            httpContext.Response.ContentType = "text/event-stream";
            httpContext.Response.Headers.CacheControl = "no-cache";
            httpContext.Response.Headers.Connection = "keep-alive";

            await foreach (var chunk in agentApp.StreamChatAsync(request, ct))
            {
                var eventType = chunk.Type switch
                {
                    "meta" => "meta",
                    "content" => "content",
                    "reasoning" => "reasoning",
                    "tool_call" => "tool_call",
                    "tool_result" => "tool_result",
                    "error" => "error",
                    "done" => "done",
                    _ => "content"
                };

                await httpContext.Response.WriteAsync($"event: {eventType}\n", ct);
                await httpContext.Response.WriteAsync($"data: {JsonHelper.Serialize(chunk)}\n\n", ct);
                await httpContext.Response.Body.FlushAsync(ct);
            }
        }).WithName("agent_stream_chat");

        // 技能列表
        group.MapGet("skills", async (ISkilCapable skilCapable) =>
        {
            var skills = skilCapable.Skills.Select(s => new
            {
                s.Name,
                s.Description
            }).ToList();

            return Results.Ok(skills);
        }).WithName("agent_skills");
    }
}
