using SpeakEase.Write.Infrastructure.AI.Contract;

namespace SpeakEase.Write.Infrastructure.AI.Agents.Contract;

public interface IOutlineAgent : INovelAgent
{
    string OutlineDomain { get; }
}
