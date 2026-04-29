using SpeakEase.Write.Infrastructure.AI.Contract;

namespace SpeakEase.Write.Infrastructure.AI.Agents.Contract;

public interface IWriteAgent : INovelAgent
{
    string WritingStyle { get; }
}
