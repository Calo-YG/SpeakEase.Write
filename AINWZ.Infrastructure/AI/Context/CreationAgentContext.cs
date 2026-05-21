using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Infrastructure.AI.Memory;

namespace SpeakEase.Write.Infrastructure.AI.Context;

public sealed class CreationAgentContext : ICreationAgentContext
{
    private readonly IMemoryProvider _memory;
    private readonly IUserContext _user;

    public CreationAgentContext(
        IMemoryProvider memory,
        IUserContext user)
    {
        _memory = memory;
        _user = user;
    }

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
    //public async Task<ChatMessage> HistoryMessaes(string sessionId, CancellationToken cancellationToken = default)
    //{

    //}
}
