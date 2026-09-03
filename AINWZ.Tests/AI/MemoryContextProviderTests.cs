using SpeakEase.Write.Application.Abstractions.Memory;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Infrastructure.AI.Memory;

namespace AINWZ.Tests.AI;

public sealed class MemoryContextProviderTests
{
    [Fact]
    public async Task LoadAsync_ReturnsOnlyArtifactsFromCompletedOwnedWorkRuns()
    {
        await using var db = TestDb.Create();
        db.AgentRuns.AddRange(
            Run("run-completed", "user-1", "work-1", "completed"),
            Run("run-running", "user-1", "work-1", "running"),
            Run("run-other", "user-2", "work-1", "completed"));
        db.AgentArtifacts.AddRange(
            Artifact("artifact-good", "run-completed"),
            Artifact("artifact-running", "run-running"),
            Artifact("artifact-other", "run-other"));
        await db.SaveChangesAsync();
        var provider = new MemoryContextProvider(new FakeMemoryProvider(), db);

        var layers = await provider.LoadAsync(new MemoryContextRequest
        {
            UserId = "user-1", WorkId = "work-1", SessionId = "session-1"
        });

        Assert.Single(layers.RetrievedArtifacts);
        Assert.Equal("artifact-good", layers.RetrievedArtifacts[0].ArtifactId);
    }

    private static AgentRunEntity Run(string id, string userId, string workId, string status)
        => new()
        {
            Id = id,
            UserId = userId,
            WorkId = workId,
            SessionId = "session-1",
            DeduplicationKey = id,
            Status = status
        };

    private static AgentArtifactEntity Artifact(string id, string runId)
        => new()
        {
            Id = id,
            UserId = "user-1",
            RunId = runId,
            StepId = "step-1",
            ContentType = "text/markdown",
            Summary = id
        };
}
