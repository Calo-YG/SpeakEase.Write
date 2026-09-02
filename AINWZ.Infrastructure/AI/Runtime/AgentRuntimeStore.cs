using Microsoft.EntityFrameworkCore;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.AI.Lib.Runtime;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Application.Abstractions.Identity;
using SpeakEase.Write.Application.Abstractions.Ids;
using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEase.Write.Domain.Entities.AI;

namespace SpeakEase.Write.Infrastructure.AI.Runtime;

public sealed class AgentRuntimeStore(
    IAgentRunStore runStore,
    IAgentRuntimeDbContext db,
    IUserContext userContext,
    ISnowflakeIdGenerator idGenerator) : IAgentRuntimeStore
{
    private readonly IAgentRunStore _runStore = runStore ?? throw new ArgumentNullException(nameof(runStore));
    private readonly IAgentRuntimeDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly IUserContext _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    private readonly ISnowflakeIdGenerator _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));

    public Task<AgentRunStartResult> StartAsync(string workId, string sessionId, string deduplicationKey, string clientMessageId, CancellationToken cancellationToken = default)
        => _runStore.StartAsync(workId, sessionId, deduplicationKey, clientMessageId, cancellationToken);

    public Task CompleteAsync(string runId, AgentResponse response, CancellationToken cancellationToken = default)
        => _runStore.CompleteAsync(runId, response, cancellationToken);

    public Task AppendEventAsync(string runId, string stepId, long sequence, string type, object payload, CancellationToken cancellationToken = default)
        => _runStore.AppendEventAsync(runId, stepId, sequence, type, payload, cancellationToken);

    public Task SaveArtifactAsync(string runId, string stepId, string contentType, string summary, string content, int estimatedTokens, CancellationToken cancellationToken = default)
        => _runStore.SaveArtifactAsync(runId, stepId, contentType, summary, content, estimatedTokens, cancellationToken);

    public Task RecordToolCallAsync(string runId, string stepId, ToolCall toolCall, CancellationToken cancellationToken = default)
        => _runStore.RecordToolCallAsync(runId, stepId, toolCall, cancellationToken);

    public Task<ToolExecutionLease> BeginAsync(string runId, string stepId, string executionKey, ToolCall toolCall, CancellationToken cancellationToken = default)
        => _runStore.BeginAsync(runId, stepId, executionKey, toolCall, cancellationToken);

    public Task CompleteAsync(string runId, string stepId, string executionKey, ToolCall toolCall, ToolResult result, CancellationToken cancellationToken = default)
        => _runStore.CompleteAsync(runId, stepId, executionKey, toolCall, result, cancellationToken);

    public async Task SaveCheckpointAsync(AgentCheckpointDto checkpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (string.IsNullOrWhiteSpace(checkpoint.RunId) || string.IsNullOrWhiteSpace(checkpoint.StepId))
            throw new ArgumentException("Checkpoint RunId and StepId are required.", nameof(checkpoint));

        var userId = _userContext.UserId;
        var existing = await _db.AgentCheckpoints.FirstOrDefaultAsync(x =>
            x.UserId == userId && x.RunId == checkpoint.RunId && x.StepId == checkpoint.StepId,
            cancellationToken);
        if (existing is not null && existing.Version >= checkpoint.Version)
            return;

        var now = DateTime.Now;
        if (existing is null)
        {
            existing = new AgentCheckpointEntity
            {
                Id = string.IsNullOrWhiteSpace(checkpoint.Id) ? _idGenerator.NextIdString() : checkpoint.Id,
                UserId = userId,
                RunId = checkpoint.RunId,
                StepId = checkpoint.StepId,
                CreateBy = userId,
                CreateAt = now
            };
            _db.AgentCheckpoints.Add(existing);
        }

        existing.State = checkpoint.State ?? string.Empty;
        existing.MessagesJson = checkpoint.MessagesJson ?? string.Empty;
        existing.Iteration = checkpoint.Iteration;
        existing.PendingToolCallsJson = checkpoint.PendingToolCallsJson ?? string.Empty;
        existing.Version = checkpoint.Version;
        existing.UpdateBy = userId;
        existing.UpdateAt = now;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AgentCheckpointDto> LoadCheckpointAsync(string runId, string stepId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.AgentCheckpoints.AsNoTracking().FirstOrDefaultAsync(x =>
            x.UserId == _userContext.UserId && x.RunId == runId && x.StepId == stepId,
            cancellationToken);
        if (entity is null)
            return null;

        return new AgentCheckpointDto
        {
            Id = entity.Id,
            RunId = entity.RunId,
            StepId = entity.StepId,
            State = entity.State,
            MessagesJson = entity.MessagesJson,
            Iteration = entity.Iteration,
            PendingToolCallsJson = entity.PendingToolCallsJson,
            Version = entity.Version
        };
    }
}
