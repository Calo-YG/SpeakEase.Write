using SpeakEase.Write.Infrastructure.AI.Contract;

namespace SpeakEase.Write.Infrastructure.AI.Agents.Contract;

// 世界观Agent契约：继承INovelAgent，扩展世界观领域标识
public interface IWorldAgent : INovelAgent
{
    string WorldDomain { get; } // 世界观领域
}
