using SpeakEase.Write.Infrastructure.AI.Contract;

namespace SpeakEase.Write.Infrastructure.AI.Agents.Contract;

// 审核Agent契约：继承INovelAgent，扩展审核范围标识
public interface IAuditAgent : INovelAgent
{
    string AuditScope { get; } // 审核范围，如"all"表示审核全部维度
}
