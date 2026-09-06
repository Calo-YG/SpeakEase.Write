namespace SpeakEase.AI.Lib.Runtime;

public interface IStepScheduler
{
    IReadOnlyList<RuntimePlanStep> Order(IReadOnlyList<RuntimePlanStep> steps);
}
