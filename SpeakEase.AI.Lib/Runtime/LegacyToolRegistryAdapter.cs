using SpeakEase.AI.Lib.Contract;

namespace SpeakEase.AI.Lib.Runtime;

public sealed class LegacyToolRegistryAdapter : IToolRegistry
{
    private readonly ToolRegistry _registry = new();

    public LegacyToolRegistryAdapter(IToolCapable tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        foreach (var definition in tools.Tools)
            _registry.Register(definition);
    }

    public IReadOnlyList<ToolCapabilityDescriptor> All => _registry.All;

    public ToolCapabilityDescriptor Get(string name) => _registry.Get(name);

    public IReadOnlyList<SpeakEase.AI.Lib.OpenAIModel.ToolDefinition> GetExposed(ToolExposureContext context)
        => _registry.GetExposed(context);
}
