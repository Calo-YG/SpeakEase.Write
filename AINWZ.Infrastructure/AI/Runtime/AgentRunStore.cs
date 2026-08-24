using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.AI.Lib.Runtime;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Application.Abstractions.Identity;
using SpeakEase.Write.Application.Abstractions.Ids;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Runtime;

public sealed class AgentRunStore(
    SpeakEaseDbContext db,
    IUserContext userContext,
    ISnowflakeIdGenerator idGenerator) : IAgentRunStore
{
    public async Task<AgentRunStartResult> StartAsync(
        string workId,
        string sessionId,
        string deduplicationKey,
        string clientMessageId,
        CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        var existing = await db.AgentRuns
            .FirstOrDefaultAsync(x => x.UserId == userId &&
                                      x.WorkId == workId &&
                                      x.SessionId == sessionId &&
                                      x.DeduplicationKey == deduplicationKey,
                cancellationToken);

        if (existing is not null)
        {
            return new AgentRunStartResult
            {
                RunId = existing.Id,
                IsReplay = existing.Status == "completed",
                IsInProgress = existing.Status == "running",
                ExistingResponse = existing.Status == "completed"
                    ? DeserializeResponse(existing.ResultJson, existing.Content, existing.StopReason)
                    : null
            };
        }

        var now = DateTime.Now;
        var entity = new AgentRunEntity
        {
            Id = idGenerator.NextIdString(),
            UserId = userId,
            WorkId = workId,
            SessionId = sessionId,
            DeduplicationKey = deduplicationKey,
            ClientMessageId = clientMessageId ?? string.Empty,
            Status = "running",
            StartedAt = now,
            CreateBy = userId,
            CreateAt = now,
            UpdateBy = userId,
            UpdateAt = now
        };

        db.AgentRuns.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var concurrent = await db.AgentRuns.AsNoTracking().FirstAsync(x =>
                x.UserId == userId && x.WorkId == workId && x.SessionId == sessionId &&
                x.DeduplicationKey == deduplicationKey, cancellationToken);
            return new AgentRunStartResult
            {
                RunId = concurrent.Id,
                IsReplay = concurrent.Status == "completed",
                IsInProgress = concurrent.Status == "running",
                ExistingResponse = concurrent.Status == "completed"
                    ? DeserializeResponse(concurrent.ResultJson, concurrent.Content, concurrent.StopReason)
                    : null
            };
        }

        return new AgentRunStartResult { RunId = entity.Id };
    }

    public async Task CompleteAsync(
        string runId,
        AgentResponse response,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.AgentRuns.FirstOrDefaultAsync(x => x.Id == runId, cancellationToken);
        if (entity is null)
            return;

        var now = DateTime.Now;
        entity.StopReason = response?.StopReason ?? "failed";
        entity.Status = entity.StopReason switch
        {
            "completed" => "completed",
            "cancelled" => "cancelled",
            "timed_out" => "timed_out",
            _ => "failed"
        };
        entity.Content = response?.Content ?? string.Empty;
        entity.Model = response?.Model ?? string.Empty;
        entity.ResultJson = JsonSerializer.Serialize(response);
        entity.CompletedAt = now;
        entity.UpdateAt = now;
        entity.UpdateBy = userContext.UserId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendEventAsync(
        string runId,
        string stepId,
        long sequence,
        string type,
        object payload,
        CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        db.AgentRunEvents.Add(new AgentRunEventEntity
        {
            Id = idGenerator.NextIdString(),
            UserId = userId,
            RunId = runId,
            StepId = stepId ?? string.Empty,
            Sequence = sequence,
            Type = type ?? string.Empty,
            PayloadJson = JsonSerializer.Serialize(payload),
            CreateBy = userId,
            CreateAt = DateTime.Now,
            UpdateBy = userId,
            UpdateAt = DateTime.Now
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveArtifactAsync(
        string runId,
        string stepId,
        string contentType,
        string summary,
        string content,
        int estimatedTokens,
        CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        var entity = await db.AgentArtifacts.FirstOrDefaultAsync(x =>
            x.RunId == runId && x.StepId == stepId, cancellationToken);
        var now = DateTime.Now;
        entity ??= new AgentArtifactEntity
        {
            Id = idGenerator.NextIdString(),
            UserId = userId,
            RunId = runId,
            StepId = stepId,
            CreateBy = userId,
            CreateAt = now
        };
        if (entity.Id is not null && db.Entry(entity).State == EntityState.Detached)
            db.AgentArtifacts.Add(entity);
        entity.ContentType = contentType ?? "plain";
        entity.Summary = summary ?? string.Empty;
        entity.Content = content ?? string.Empty;
        entity.EstimatedTokens = estimatedTokens;
        entity.UpdateBy = userId;
        entity.UpdateAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordToolCallAsync(
        string runId,
        string stepId,
        ToolCall toolCall,
        CancellationToken cancellationToken = default)
    {
        if (toolCall is null)
            return;

        var userId = userContext.UserId;
        var entity = await db.AgentToolCalls.FirstOrDefaultAsync(x =>
            x.RunId == runId && x.ToolCallId == toolCall.Id, cancellationToken);
        var now = DateTime.Now;
        entity ??= new AgentToolCallEntity
        {
            Id = idGenerator.NextIdString(),
            UserId = userId,
            RunId = runId,
            StepId = stepId ?? string.Empty,
            ToolCallId = toolCall.Id ?? string.Empty,
            ToolName = toolCall.Function?.Name ?? string.Empty,
            ArgumentsHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(toolCall.Function?.Arguments ?? string.Empty))),
            Status = "requested",
            CreateBy = userId,
            CreateAt = now
        };
        if (entity.Id is not null && db.Entry(entity).State == EntityState.Detached)
            db.AgentToolCalls.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ToolExecutionLease> BeginAsync(
        string runId,
        string stepId,
        ToolCall toolCall,
        CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        var existing = await db.AgentToolCalls.FirstOrDefaultAsync(x =>
            x.UserId == userId && x.RunId == runId && x.ToolCallId == (toolCall.Id ?? string.Empty),
            cancellationToken);

        if (existing is not null)
        {
            if (existing.Status == "completed" && !string.IsNullOrWhiteSpace(existing.ResultJson))
            {
                try
                {
                    var replay = JsonSerializer.Deserialize<ToolResult>(existing.ResultJson);
                    if (replay is not null)
                        return ToolExecutionLease.Replay(replay);
                }
                catch (JsonException)
                {
                }
            }

            return ToolExecutionLease.Replay(ToolResult.Fail(
                "This tool call is already executing or has no replayable result.",
                "tool_call_in_progress"));
        }

        var now = DateTime.Now;
        db.AgentToolCalls.Add(new AgentToolCallEntity
        {
            Id = idGenerator.NextIdString(),
            UserId = userId,
            RunId = runId,
            StepId = stepId ?? string.Empty,
            ToolCallId = toolCall.Id ?? string.Empty,
            ToolName = toolCall.Function?.Name ?? string.Empty,
            ArgumentsHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(toolCall.Function?.Arguments ?? string.Empty))),
            Status = "running",
            CreateBy = userId,
            CreateAt = now,
            UpdateBy = userId,
            UpdateAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
        return ToolExecutionLease.Execute();
    }

    public async Task CompleteAsync(
        string runId,
        string stepId,
        ToolCall toolCall,
        ToolResult result,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.AgentToolCalls.FirstOrDefaultAsync(x =>
            x.UserId == userContext.UserId && x.RunId == runId && x.ToolCallId == (toolCall.Id ?? string.Empty),
            cancellationToken);
        if (entity is null)
            return;

        entity.Status = result?.Success == true ? "completed" : "failed";
        entity.ResultJson = JsonSerializer.Serialize(result);
        entity.UpdateBy = userContext.UserId;
        entity.UpdateAt = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static AgentResponse DeserializeResponse(string json, string content, string stopReason)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var response = JsonSerializer.Deserialize<AgentResponse>(json);
                if (response is not null)
                    return response;
            }
            catch (JsonException)
            {
            }
        }

        return new AgentResponse
        {
            Content = content ?? string.Empty,
            StopReason = stopReason ?? "completed",
            RunStatus = "completed"
        };
    }
}
