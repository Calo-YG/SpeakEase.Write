using SpeakEase.Write.Infrastructure.AI.Contract;

namespace SpeakEase.Write.Infrastructure.AI.Agents.Contract;

// 大纲Agent契约：继承INovelAgent，扩展大纲领域标识
public interface IOutlineAgent : INovelAgent
{
    string OutlineDomain { get; } // 大纲领域
}
