using SpeakEase.Write.Infrastructure.AI.Contract;

namespace SpeakEase.Write.Infrastructure.AI.Agents.Contract;

// 创作Agent契约：继承INovelAgent，扩展创作领域标识
public interface ICreationAgent : INovelAgent
{
    string CreationDomain { get; } // 创作领域，如"角色设计与创意生成"
}
