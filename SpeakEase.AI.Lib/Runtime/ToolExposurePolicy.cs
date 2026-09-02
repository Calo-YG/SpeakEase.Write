using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.AI.Lib.Runtime;

public sealed class ToolExposurePolicy(IToolRegistry registry)
{
    private readonly IToolRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public IReadOnlyList<ToolDefinition> Select(ToolExposureContext context)
    {
        return _registry.GetExposed(context);
    }
}
