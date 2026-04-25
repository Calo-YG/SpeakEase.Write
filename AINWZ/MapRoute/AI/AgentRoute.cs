using SpeakEase.Write.Application.Contracts.AI;
using SpeakEase.Write.Application.Contracts.AI.Dto;

namespace SpeakEase.Write.MapRoute.AI;

public static class AgentRoute
{
    public static void MapAgentEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("ai/agent")
            .WithDescription("Agent 对话")
            .WithTags("agent")
            .RequireAuthorization();

        group.MapPost("chat", async (AgentChatRequestDto request, IAgentApplication agentApp, CancellationToken ct) =>
            await agentApp.ChatAsync(request, ct))
            .WithName("agentchat");
    }
}
