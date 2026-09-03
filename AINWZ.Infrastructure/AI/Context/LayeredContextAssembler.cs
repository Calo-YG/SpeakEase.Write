using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.Write.Infrastructure.AI.Context;

public sealed class LayeredContextAssembler
{
    private const int DefaultContextWindowTokens = 32_000;
    private const int ReservedOutputTokens = 8_000;

    public LayeredContextAssemblyResult Assemble(LayeredContextAssemblyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var inputBudget = ResolveInputBudget(request.ContextWindowTokens);
        var historyBudget = Math.Max(0, inputBudget - EstimateTokens(request.CurrentUserMessage));
        var l3 = CreateSystem("[Project Facts]", request.ProjectFacts);
        var l2 = CreateSystem("[Session Memory]", request.SessionMemory);
        var l4 = CreateSystem("[Retrieved Context]", request.RetrievedContext);
        var turns = request.ConversationTurns.ToList();
        var wasTrimmed = false;

        var messages = BuildMessages(l3, l2, l4, turns);
        if (EstimateTokens(messages) > historyBudget && l4 is not null)
        {
            l4 = null;
            wasTrimmed = true;
            messages = BuildMessages(l3, l2, l4, turns);
        }

        if (EstimateTokens(messages) > historyBudget && l2 is not null)
        {
            var otherTokens = EstimateTokens(BuildMessages(l3, null, null, turns));
            l2.Content = TruncateToBudget(l2.Content, Math.Max(0, historyBudget - otherTokens));
            if (string.IsNullOrWhiteSpace(l2.Content))
                l2 = null;
            wasTrimmed = true;
            messages = BuildMessages(l3, l2, null, turns);
        }

        while (turns.Count > 0 && EstimateTokens(messages) > historyBudget)
        {
            turns.RemoveAt(0);
            wasTrimmed = true;
            messages = BuildMessages(l3, l2, null, turns);
        }

        if (EstimateTokens(messages) > historyBudget && l3 is not null)
        {
            var memoryTokens = l2 is null ? 0 : EstimateTokens(l2.Content);
            l3.Content = TruncateToBudget(l3.Content, Math.Max(0, historyBudget - memoryTokens));
            if (string.IsNullOrWhiteSpace(l3.Content))
                l3 = null;
            wasTrimmed = true;
            messages = BuildMessages(l3, l2, null, turns);
        }

        return new LayeredContextAssemblyResult
        {
            Messages = messages,
            InputTokenCount = EstimateTokens(messages) + EstimateTokens(request.CurrentUserMessage),
            WasTrimmed = wasTrimmed,
            RetainedTurns = turns.Count
        };
    }

    public static int EstimateTokens(IEnumerable<ChatMessage> messages)
        => messages.Sum(message => EstimateTokens(ExtractText(message)));

    public static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var chineseCount = text.Count(c => c is >= '\u4E00' and <= '\u9FFF');
        return (int)(chineseCount * 1.5) + (int)((text.Length - chineseCount) * 0.25);
    }

    private static List<ChatMessage> BuildMessages(
        SystemMessage l3,
        SystemMessage l2,
        SystemMessage l4,
        IReadOnlyList<LayeredConversationTurn> turns)
    {
        var messages = new List<ChatMessage>();
        if (l3 is not null)
            messages.Add(l3);
        if (l2 is not null)
            messages.Add(l2);
        if (l4 is not null)
            messages.Add(l4);
        messages.AddRange(turns.SelectMany(x => x.Messages));
        return messages;
    }

    private static SystemMessage CreateSystem(string heading, string content)
        => string.IsNullOrWhiteSpace(content) ? null : ChatMessage.System($"{heading}\n{content}");

    private static int ResolveInputBudget(int contextWindowTokens)
    {
        var window = contextWindowTokens > 0 ? contextWindowTokens : DefaultContextWindowTokens;
        var reservedOutput = Math.Min(ReservedOutputTokens, Math.Max(1_000, window / 4));
        return Math.Max(1, window - reservedOutput);
    }

    private static string ExtractText(ChatMessage message)
        => message switch
        {
            SystemMessage system => system.Content,
            UserMessage user when user.Content is string value => value,
            UserMessage user => user.Content?.ToString() ?? string.Empty,
            AssistantMessage assistant => assistant.Content ?? string.Empty,
            ToolMessage tool => tool.Content ?? string.Empty,
            _ => string.Empty
        };

    private static string TruncateToBudget(string value, int budget)
    {
        if (string.IsNullOrEmpty(value) || budget <= 0)
            return string.Empty;

        var low = 0;
        var high = value.Length;
        var best = string.Empty;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var candidate = value[..middle];
            if (EstimateTokens(candidate) <= budget)
            {
                best = candidate;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }
        return best;
    }
}

public sealed class LayeredContextAssemblyRequest
{
    public string CurrentUserMessage { get; init; } = string.Empty;
    public string ProjectFacts { get; init; } = string.Empty;
    public string SessionMemory { get; init; } = string.Empty;
    public string RetrievedContext { get; init; } = string.Empty;
    public IReadOnlyList<LayeredConversationTurn> ConversationTurns { get; init; } = Array.Empty<LayeredConversationTurn>();
    public int ContextWindowTokens { get; init; }
}

public sealed class LayeredConversationTurn
{
    public int TurnNumber { get; init; }
    public IReadOnlyList<ChatMessage> Messages { get; init; } = Array.Empty<ChatMessage>();
}

public sealed class LayeredContextAssemblyResult
{
    public List<ChatMessage> Messages { get; init; } = new();
    public int InputTokenCount { get; init; }
    public int RetainedTurns { get; init; }
    public bool WasTrimmed { get; init; }
}
