using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.AI.Lib.Runtime;

public interface IToolRegistry
{
    IReadOnlyList<ToolCapabilityDescriptor> All { get; }

    ToolCapabilityDescriptor Get(string name);

    IReadOnlyList<ToolDefinition> GetExposed(ToolExposureContext context);
}
