using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.AI.Lib.Runtime;

public sealed class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ToolCapabilityDescriptor> _items = new(StringComparer.Ordinal);

    public IReadOnlyList<ToolCapabilityDescriptor> All => _items.Values.ToArray();

    public void Register(ToolDefinition definition, ToolCapabilityDescriptor descriptor = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(definition.Function);

        var name = definition.Function.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tool definition must have a function name.", nameof(definition));

        var metadata = descriptor ?? CreateDefaultDescriptor(definition);
        if (!string.Equals(metadata.Name, name, StringComparison.Ordinal))
        {
            metadata = new ToolCapabilityDescriptor
            {
                Name = name,
                Group = metadata.Group,
                RiskLevel = metadata.RiskLevel,
                ReadOnly = metadata.ReadOnly,
                RequiresExplicitConsent = metadata.RequiresExplicitConsent,
                RequiredScopes = metadata.RequiredScopes,
                RequiredPhases = metadata.RequiredPhases,
                Definition = definition
            };
        }
        else if (metadata.Definition is null)
        {
            metadata = new ToolCapabilityDescriptor
            {
                Name = metadata.Name,
                Group = metadata.Group,
                RiskLevel = metadata.RiskLevel,
                ReadOnly = metadata.ReadOnly,
                RequiresExplicitConsent = metadata.RequiresExplicitConsent,
                RequiredScopes = metadata.RequiredScopes,
                RequiredPhases = metadata.RequiredPhases,
                Definition = definition
            };
        }

        _items[name] = metadata;
    }

    public ToolCapabilityDescriptor Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return _items.GetValueOrDefault(name.Trim());
    }

    public IReadOnlyList<ToolDefinition> GetExposed(ToolExposureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _items.Values
            .Where(x => IsAllowed(x, context))
            .OrderBy(x => PreferredIndex(x.Name, context.PreferredTools))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .Take(Math.Max(1, context.MaxTools))
            .Select(x => x.Definition)
            .ToArray();
    }

    private static bool IsAllowed(ToolCapabilityDescriptor descriptor, ToolExposureContext context)
    {
        if (context.AllowedGroups.Count > 0 && !context.AllowedGroups.Contains(descriptor.Group, StringComparer.Ordinal))
            return false;

        if (descriptor.RequiresExplicitConsent && !context.HasExplicitConsent)
            return false;

        if (!string.Equals(context.Phase, "run", StringComparison.OrdinalIgnoreCase) &&
            descriptor.RequiredPhases.Count > 0 &&
            !descriptor.RequiredPhases.Contains(context.Phase, StringComparer.OrdinalIgnoreCase))
            return false;

        return descriptor.RequiredScopes.All(scope => context.GrantedScopes.Contains(scope, StringComparer.Ordinal));
    }

    private static int PreferredIndex(string name, IReadOnlyList<string> preferredTools)
    {
        for (var index = 0; index < preferredTools.Count; index++)
        {
            if (string.Equals(name, preferredTools[index], StringComparison.Ordinal))
                return index;
        }

        return int.MaxValue;
    }

    private static ToolCapabilityDescriptor CreateDefaultDescriptor(ToolDefinition definition)
    {
        var name = definition.Function.Name;
        var isHighRisk = name is "run_powershell" or "web_search" or "find_skill";
        var isGraph = name.StartsWith("create_character_graph", StringComparison.Ordinal);
        var isRead = name.StartsWith("get_", StringComparison.Ordinal)
                     || name.StartsWith("search_", StringComparison.Ordinal)
                     || name.StartsWith("list_", StringComparison.Ordinal);
        var group = isHighRisk
            ? "system.high-risk"
            : isGraph
                ? "graph.internal"
                : name switch
                {
                    "create_character_arc" or "get_character_arc" => "character.growth",
                    "create_relationship" => "relationship.write",
                    "get_relationships" => "relationship.read",
                    _ when isRead => "system.legacy.read",
                    _ when name.StartsWith("create_character", StringComparison.Ordinal)
                        || name == "update_character" => "character.write",
                    _ when name.StartsWith("save_chapter", StringComparison.Ordinal)
                        || name == "update_chapter_summary" => "chapter.write",
                    _ => "system.legacy"
                };

        return new ToolCapabilityDescriptor
        {
            Name = name,
            Group = group,
            RiskLevel = isHighRisk ? "high" : "medium",
            ReadOnly = isRead,
            RequiresExplicitConsent = isHighRisk,
            RequiredPhases = isRead ? new[] { "context_loading", "generate", "review" } : new[] { "generate", "commit" },
            Definition = definition
        };
    }
}
