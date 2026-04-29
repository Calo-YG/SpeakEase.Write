using SpeakEase.Write.Infrastructure.AI.Contract;

namespace SpeakEase.Write.Infrastructure.AI.Agents.Contract;

public interface IWorldAgent : INovelAgent
{
    string WorldDomain { get; }
}
