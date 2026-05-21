using Microsoft.EntityFrameworkCore;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Infrastructure.AI.Memory;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Context;

public sealed class CreationAgentContext(
    IMemoryProvider memory,
    IUserContext user,
    SpeakEaseDbContext dbContext) : ICreationAgentContext
{
    private readonly IMemoryProvider _memory = memory;
    private readonly IUserContext _user = user;

    public async Task<AgentContext> BuildContext(string workId, CancellationToken cancellationToken = default)
    {
        var ctx = new AgentContext
        {
            HistoryMessage = new List<string>(),
            RequestId = Guid.NewGuid().ToString()
        };

        if (string.IsNullOrEmpty(workId))
        {
            ctx.ProjectMemory = string.Empty;
            return ctx;
        }

        // var mem = await _memory.LoadAsync(_user.UserId, workId, cancellationToken);

        //ctx.ProjectMemory = FormatProjectMemory(mem);

        return ctx;
    }

    /// <summary>
    /// 历史消息构建
    /// </summary>
    /// <param name="sessionId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<ChatMessage>> HistoryMessaes(string sessionId, CancellationToken cancellationToken = default)
    {
        var messages = await dbContext.AICreationMessages.Where(m => m.SessionId == sessionId && m.Role != "tool")
            .OrderBy(m => m.CreatedAt)
            .Take(20)
            .ToListAsync();

        List<ChatMessage> chatMessage = [];

        foreach (var message in messages.OrderBy(m => m.CreatedAt))
        {
            var role = message.Role == "user" ? "user" : "assistant";

            if (role == "user")
            {
                chatMessage.Add(ChatMessage.User(message.Content));
            } else if (role == "assistant")
            {
                chatMessage.Add(ChatMessage.Assistant(message.Content));
            }
        }

        return chatMessage;
    }
}
