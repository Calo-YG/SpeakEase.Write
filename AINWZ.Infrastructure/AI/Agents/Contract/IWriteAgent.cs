using SpeakEase.Write.Infrastructure.AI.Contract;

namespace SpeakEase.Write.Infrastructure.AI.Agents.Contract;

// 写作Agent契约：继承INovelAgent，扩展写作风格标识
public interface IWriteAgent : INovelAgent
{
    string WritingStyle { get; } // 写作风格，如"文学性创作"
}
