using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.AI.Lib.Runtime;

public sealed class ExposedToolCapable(
    IToolCapable inner,
    IReadOnlyList<ToolDefinition> exposedTools) : IToolCapable
{
    private readonly IToolCapable _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly HashSet<string> _allowedNames = new(
        (exposedTools ?? Array.Empty<ToolDefinition>()).Select(x => x.Function.Name),
        StringComparer.Ordinal);

    public IReadOnlyList<ToolDefinition> Tools { get; } = exposedTools ?? Array.Empty<ToolDefinition>();

    public void RegisterTool(ToolDefinition tool)
        => throw new NotSupportedException("The exposed tool view is immutable.");

    public Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken)
    {
        var name = toolCall?.Function?.Name;
        if (string.IsNullOrWhiteSpace(name) || !_allowedNames.Contains(name))
        {
            var result = ToolResult.Fail("Tool is not exposed for this runtime step.", "tool_not_exposed");
            result.ToolCallId = toolCall?.Id;
            result.ToolName = name;
            return Task.FromResult(result);
        }

        return _inner.ExecuteAsync(toolCall, cancellationToken);
    }
}
