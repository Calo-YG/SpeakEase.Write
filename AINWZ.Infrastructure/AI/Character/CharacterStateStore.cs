using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeakEase.Write.Application.Abstractions.Identity;
using SpeakEase.Write.Application.Abstractions.Ids;
using SpeakEase.Write.Application.Abstractions.Story;
using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEase.Write.Domain.Entities.Story;

namespace SpeakEase.Write.Infrastructure.AI.Character;

public sealed class CharacterStateStore(
    ICharacterDbContext db,
    IUserContext userContext,
    ISnowflakeIdGenerator idGenerator) : ICharacterStateStore
{
    private readonly ICharacterDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly IUserContext _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    private readonly ISnowflakeIdGenerator _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));

    public async Task<CharacterStateSnapshotData> EnsureBaselineAsync(string workId, string characterId, CancellationToken cancellationToken = default)
        => await EnsureBaselineAsync(_userContext.UserId, workId, characterId, cancellationToken);

    public async Task<CharacterStateSnapshotData> EnsureBaselineAsync(
        string userId,
        string workId,
        string characterId,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetLatestSnapshotAsync(userId, workId, characterId, cancellationToken);
        if (existing is not null)
            return existing;

        var character = await _db.Characters.AsNoTracking().FirstOrDefaultAsync(x =>
            x.WorkId == workId && x.Id == characterId && x.OwnerId == userId,
            cancellationToken);
        if (character is null)
            return null;

        var state = JsonSerializer.Serialize(new
        {
            character.Personality,
            character.Motivation,
            character.AbilityDescription,
            goals = Array.Empty<string>(),
            conflicts = Array.Empty<string>()
        }, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        var baseline = new CharacterStateSnapshotData
        {
            UserId = userId,
            WorkId = workId,
            CharacterId = characterId,
            StateJson = state,
            Status = "confirmed",
            Version = 0
        };
        await SaveSnapshotAsync(baseline, cancellationToken);
        return await GetLatestSnapshotAsync(userId, workId, characterId, cancellationToken);
    }

    public async Task<CharacterStateSnapshotData> GetLatestSnapshotAsync(string workId, string characterId, CancellationToken cancellationToken = default)
        => await GetLatestSnapshotAsync(_userContext.UserId, workId, characterId, cancellationToken);

    public async Task<CharacterStateSnapshotData> GetLatestSnapshotAsync(
        string userId,
        string workId,
        string characterId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.CharacterStateSnapshots.AsNoTracking().FirstOrDefaultAsync(x =>
            x.WorkId == workId && x.CharacterId == characterId && x.UserId == userId,
            cancellationToken);
        return entity is null ? null : ToSnapshotData(entity);
    }

    public async Task SaveSnapshotAsync(CharacterStateSnapshotData snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var userId = ResolveUserId(snapshot.UserId);
        var entity = await _db.CharacterStateSnapshots.FirstOrDefaultAsync(x =>
            x.WorkId == snapshot.WorkId && x.CharacterId == snapshot.CharacterId && x.UserId == userId,
            cancellationToken);
        if (entity is not null && entity.Version >= snapshot.Version)
            return;

        var now = DateTime.Now;
        if (entity is null)
        {
            entity = new CharacterStateSnapshotEntity
            {
                Id = string.IsNullOrWhiteSpace(snapshot.Id) ? _idGenerator.NextIdString() : snapshot.Id,
                UserId = userId,
                WorkId = snapshot.WorkId,
                CharacterId = snapshot.CharacterId,
                CreateBy = userId,
                CreateAt = now
            };
            _db.CharacterStateSnapshots.Add(entity);
        }

        entity.BasedOnEventId = snapshot.BasedOnEventId ?? string.Empty;
        entity.StateJson = snapshot.StateJson ?? string.Empty;
        entity.Version = snapshot.Version;
        entity.Status = snapshot.Status ?? "confirmed";
        entity.UpdateBy = userId;
        entity.UpdateAt = now;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> AppendEventAsync(CharacterStateEventData stateEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stateEvent);
        var userId = ResolveUserId(stateEvent.UserId);
        var existing = await _db.CharacterStateEvents.FirstOrDefaultAsync(x =>
            x.UserId == userId && x.WorkId == stateEvent.WorkId && x.CharacterId == stateEvent.CharacterId &&
            x.SourceRunId == stateEvent.SourceRunId && x.SourceEventKey == stateEvent.SourceEventKey,
            cancellationToken);
        if (existing is not null)
            return existing.Id;

        var now = DateTime.Now;
        var entity = new CharacterStateEventEntity
        {
            Id = _idGenerator.NextIdString(),
            UserId = userId,
            WorkId = stateEvent.WorkId,
            CharacterId = stateEvent.CharacterId,
            SourceRunId = stateEvent.SourceRunId,
            SourceChapterId = stateEvent.SourceChapterId ?? string.Empty,
            SourceEventKey = stateEvent.SourceEventKey ?? string.Empty,
            EventType = stateEvent.EventType ?? string.Empty,
            EvidenceJson = stateEvent.EvidenceJson ?? string.Empty,
            ChangesJson = stateEvent.ChangesJson ?? string.Empty,
            Confidence = stateEvent.Confidence,
            Version = stateEvent.Version,
            CreateBy = userId,
            CreateAt = now,
            UpdateBy = userId,
            UpdateAt = now
        };
        _db.CharacterStateEvents.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task SaveGrowthProposalAsync(CharacterGrowthProposalData proposal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var userId = ResolveUserId(proposal.UserId);
        var entity = new CharacterGrowthProposalEntity
        {
            Id = _idGenerator.NextIdString(),
            UserId = userId,
            WorkId = proposal.WorkId,
            CharacterId = proposal.CharacterId,
            SourceRunId = proposal.SourceRunId ?? string.Empty,
            ProposalJson = proposal.ProposalJson ?? string.Empty,
            Severity = proposal.Severity ?? "normal",
            Status = proposal.Status ?? "needs_review",
            CreateBy = userId,
            CreateAt = DateTime.Now,
            UpdateBy = userId,
            UpdateAt = DateTime.Now
        };
        _db.CharacterGrowthProposals.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static CharacterStateSnapshotData ToSnapshotData(CharacterStateSnapshotEntity entity)
        => new()
        {
            UserId = entity.UserId,
            Id = entity.Id,
            WorkId = entity.WorkId,
            CharacterId = entity.CharacterId,
            BasedOnEventId = entity.BasedOnEventId,
            StateJson = entity.StateJson,
            Version = entity.Version,
            Status = entity.Status
        };

    private string ResolveUserId(string userId)
        => string.IsNullOrWhiteSpace(userId) ? _userContext.UserId : userId;
}
