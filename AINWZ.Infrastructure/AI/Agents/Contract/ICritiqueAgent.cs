using SpeakEase.Write.Infrastructure.AI.Contract;

namespace SpeakEase.Write.Infrastructure.AI.Agents.Contract;

// 文风审查Agent契约：继承INovelAgent，无额外扩展属性（纯文本审查，不需要工具）
public interface ICritiqueAgent : INovelAgent
{
}
