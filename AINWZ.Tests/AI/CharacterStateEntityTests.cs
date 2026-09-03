using Microsoft.EntityFrameworkCore;
using SpeakEase.Write.Application.Abstractions.Story;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.AI.Character;

namespace AINWZ.Tests.AI;

public sealed class CharacterStateEntityTests
{
    [Fact]
    public async Task EnsureBaselineAsync_ProjectsLegacyCharacterFields()
    {
        await using var db = TestDb.Create();
        db.Characters.Add(new CharacterEntity
        {
            Id = "char-1",
            WorkId = "work-1",
            OwnerId = "user-1",
            Name = "林舟",
            Personality = "谨慎",
            Motivation = "保护家人",
            CreateBy = "user-1",
            UpdateBy = "user-1"
        });
        await db.SaveChangesAsync();
        var store = new CharacterStateStore(db, new TestUserContext(), new SequentialIdGenerator());

        var snapshot = await store.EnsureBaselineAsync("work-1", "char-1");

        Assert.Equal("char-1", snapshot.CharacterId);
        Assert.Equal(0, snapshot.Version);
        Assert.Contains("谨慎", snapshot.StateJson);
        Assert.Single(await db.CharacterStateSnapshots.ToListAsync());
    }

    [Fact]
    public async Task SaveSnapshotAsync_DoesNotOverwriteNewerVersion()
    {
        await using var db = TestDb.Create();
        var store = new CharacterStateStore(db, new TestUserContext(), new SequentialIdGenerator());

        await store.SaveSnapshotAsync(new CharacterStateSnapshotData
        {
            WorkId = "work-1",
            CharacterId = "char-1",
            StateJson = "new",
            Version = 2,
            Status = "confirmed"
        });
        await store.SaveSnapshotAsync(new CharacterStateSnapshotData
        {
            WorkId = "work-1",
            CharacterId = "char-1",
            StateJson = "old",
            Version = 1,
            Status = "confirmed"
        });

        var snapshot = await store.GetLatestSnapshotAsync("work-1", "char-1");

        Assert.Equal(2, snapshot.Version);
        Assert.Equal("new", snapshot.StateJson);
    }
}
