namespace SpeakEase.AI.Lib.Runtime;

public sealed class LinearStepScheduler : IStepScheduler
{
    public IReadOnlyList<string> Order(IReadOnlyList<string> stepIds)
    {
        ArgumentNullException.ThrowIfNull(stepIds);
        return stepIds.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    }
}
