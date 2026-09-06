namespace SpeakEase.AI.Lib.Runtime;

public interface IAgentRuntimeRunner
{
    IAsyncEnumerable<RuntimeEvent> RunAsync(
        RuntimeRunRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<RuntimeEvent> RunPlanAsync(
        RuntimePlanRequest request,
        CancellationToken cancellationToken = default);
}
