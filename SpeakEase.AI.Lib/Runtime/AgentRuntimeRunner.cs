namespace SpeakEase.AI.Lib.Runtime;

public sealed class AgentRuntimeRunner(RuntimeHost host) : IAgentRuntimeRunner
{
    private readonly RuntimeHost _host = host ?? throw new ArgumentNullException(nameof(host));

    public IAsyncEnumerable<RuntimeEvent> RunAsync(
        RuntimeRunRequest request,
        CancellationToken cancellationToken = default)
    {
        return _host.RunAsync(request, cancellationToken);
    }
}
