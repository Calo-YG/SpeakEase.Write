using System.Text.Json;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.Runtime;

namespace SpeakEase.Write.Infrastructure.AI.Runtime;

public sealed class AgentEventSseProjector
{
    public AgentStreamChunk Project(RuntimeEvent runtimeEvent)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);

        AgentStreamChunk chunk;
        if (runtimeEvent.Payload is AgentStreamChunk source)
        {
            chunk = source;
        }
        else if (runtimeEvent.Type == "run_completed" && runtimeEvent.Payload is AgentResponse response)
        {
            chunk = new AgentStreamChunk
            {
                Type = "done",
                FinalResponse = response
            };
        }
        else if (runtimeEvent.Type == "run_failed" && runtimeEvent.Payload is AgentResponse failedResponse)
        {
            chunk = new AgentStreamChunk
            {
                Type = "done",
                FinalResponse = failedResponse
            };
        }
        else
        {
            chunk = new AgentStreamChunk
            {
                Type = runtimeEvent.Type switch
                {
                    "run_started" or "step_started" => "meta",
                    "run_cancelled" => "error",
                    _ => runtimeEvent.Type
                },
                Content = SerializePayload(runtimeEvent.Payload)
            };
        }

        chunk.RunId = runtimeEvent.RunId;
        chunk.StepId = runtimeEvent.StepId;
        chunk.Sequence = runtimeEvent.Sequence;
        return chunk;
    }

    private static string SerializePayload(object payload)
    {
        if (payload is null)
            return string.Empty;
        if (payload is string text)
            return text;

        return JsonSerializer.Serialize(payload);
    }
}
