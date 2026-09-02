namespace SpeakEase.AI.Lib.Runtime;

public interface IRuntimeEventSink
{
    Task PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default);
}
