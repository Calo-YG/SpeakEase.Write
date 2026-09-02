namespace SpeakEase.AI.Lib.Runtime;

public interface IStepScheduler
{
    IReadOnlyList<string> Order(IReadOnlyList<string> stepIds);
}
