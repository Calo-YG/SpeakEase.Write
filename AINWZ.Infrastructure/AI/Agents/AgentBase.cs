using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Contract;

namespace SpeakEase.Write.Infrastructure.AI.Agents;

public abstract class AgentBase(IChatCompatible llm, IToolCapable tools) : INovelAgent
{
    protected readonly IChatCompatible Llm = llm;
    protected readonly IToolCapable Tools = tools;

    public abstract string Name { get; }
    public abstract string DisplayName { get; }
    public abstract string BuildPrompt();

    public virtual void RegisterTools(IToolCapable toolCapable)
    {
        foreach (var def in GetToolDefinitions())
            toolCapable.RegisterTool(def);
    }

    protected abstract IEnumerable<ToolDefinition> GetToolDefinitions();

    public async IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        RegisterTools(Tools);

        var messages = BuildMessages(request);
        var ctx = new LLMTurnContext
        {
            Model = request.Model,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens
        };

        for (var i = 0; i < request.MaxIterations; i++)
        {
            LLMTurnResult turnResult = null;

            await foreach (var tc in Llm.StreamAsync(ctx, messages, Tools.Tools, cancellationToken))
            {
                switch (tc.Type)
                {
                    case "content":
                        yield return new AgentStreamChunk { Type = "content", Content = tc.Content };
                        break;
                    case "tool_call":
                        yield return new AgentStreamChunk { Type = "tool_call", ToolCallDelta = tc.ToolCallDelta };
                        break;
                    case "done":
                        turnResult = tc.TurnResult;
                        break;
                }
            }

            if (turnResult == null) continue;

            if (turnResult.HasToolCalls)
            {
                messages.Add(new AssistantMessage { Content = turnResult.Content ?? string.Empty, ToolCalls = turnResult.ToolCalls });
                foreach (var tc in turnResult.ToolCalls)
                {
                    var tr = await Tools.ExecuteAsync(tc, cancellationToken);
                    yield return new AgentStreamChunk { Type = "tool_result", ToolResult = tr };
                    messages.Add(ChatMessage.Tool(tc.Id, tr.Content ?? string.Empty));
                }
            }
            else
            {
                messages.Add(ChatMessage.Assistant(turnResult.Content));
                yield return new AgentStreamChunk
                {
                    Type = "done",
                    FinalResponse = new AgentResponse
                    {
                        Content = turnResult.Content,
                        Model = turnResult.Model,
                        Iterations = i + 1,
                        StopReason = "completed"
                    }
                };
                yield break;
            }
        }

        yield return new AgentStreamChunk
        {
            Type = "done",
            FinalResponse = new AgentResponse
            {
                Content = string.Empty,
                Model = request.Model,
                Iterations = request.MaxIterations,
                StopReason = "max_iterations_reached"
            }
        };
    }

    private static List<ChatMessage> BuildMessages(AgentRequest req)
    {
        var msgs = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(req.SystemPrompt))
        {
            msgs.Add(ChatMessage.System(req.SystemPrompt));
        }

        if (req.ConversationHistory?.Count > 0)
        {
            msgs.AddRange(req.ConversationHistory);
        }

        if (!string.IsNullOrEmpty(req.UserMessage))
        {
            msgs.Add(ChatMessage.User(req.UserMessage));
        }

        return msgs;
    }
}
