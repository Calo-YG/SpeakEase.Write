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

    public async Task<IReadOnlyList<CharacterStateSnapshotData>> GetWorkSnapshotsAsync(
        string userId,
        string workId,
        CancellationToken cancellationToken = default)
        => await _db.CharacterStateSnapshots
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.WorkId == workId && x.Status == "confirmed")
            .OrderBy(x => x.CharacterId)
            .Take(64)
            .Select(x => new CharacterStateSnapshotData
            {
                UserId = x.UserId,
                Id = x.Id,
                WorkId = x.WorkId,
                CharacterId = x.CharacterId,
                BasedOnEventId = x.BasedOnEventId,
                StateJson = x.StateJson,
                Version = x.Version,
                Status = x.Status
            })
            .ToListAsync(cancellationToken);

    public async Task SaveSnapshotAsync(CharacterStateSnapshotData snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var userId = ResolveUserId(snapshot.UserId);
        var entity = await _db.CharacterStateSnapshots.FirstOrDefaultAsync(x =>
            x.WorkId == snapshot.WorkId && x.CharacterId == snapshot.CharacterId && x.UserId == userId,
            cancellationToken);
        if (entity is not null && entity.Version >= snapshot.Version)
            return;

        var now = DateTime.UtcNow;
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

        var now = DateTime.UtcNow;
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

    public async Task<bool> TryCommitStateChangeAsync(
        CharacterStateEventData stateEvent,
        CharacterStateSnapshotData snapshot,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stateEvent);
        ArgumentNullException.ThrowIfNull(snapshot);
        var userId = ResolveUserId(stateEvent.UserId);
        var existingEvent = await _db.CharacterStateEvents.AsNoTracking().FirstOrDefaultAsync(x =>
            x.UserId == userId && x.WorkId == stateEvent.WorkId && x.CharacterId == stateEvent.CharacterId &&
            x.SourceRunId == stateEvent.SourceRunId && x.SourceEventKey == stateEvent.SourceEventKey,
            cancellationToken);
        if (existingEvent is not null)
            return true;

        var now = DateTime.UtcNow;
        var eventId = _idGenerator.NextIdString();
        var eventEntity = CreateEventEntity(stateEvent, userId, eventId, now);
        if (!_db.Database.IsRelational())
        {
            var snapshotEntity = await _db.CharacterStateSnapshots.FirstOrDefaultAsync(x =>
                x.UserId == userId && x.WorkId == snapshot.WorkId && x.CharacterId == snapshot.CharacterId,
                cancellationToken);
            if (snapshotEntity is null || snapshotEntity.Version != expectedVersion)
                return false;

            ApplySnapshot(snapshotEntity, snapshot, eventId, userId, now);
            _db.CharacterStateEvents.Add(eventEntity);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var affected = await _db.CharacterStateSnapshots
            .Where(x => x.UserId == userId && x.WorkId == snapshot.WorkId &&
                        x.CharacterId == snapshot.CharacterId && x.Version == expectedVersion)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.BasedOnEventId, eventId)
                    .SetProperty(x => x.StateJson, snapshot.StateJson ?? string.Empty)
                    .SetProperty(x => x.Version, snapshot.Version)
                    .SetProperty(x => x.Status, snapshot.Status ?? "confirmed")
                    .SetProperty(x => x.UpdateBy, userId)
                    .SetProperty(x => x.UpdateAt, now),
                cancellationToken);
        if (affected != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        _db.CharacterStateEvents.Add(eventEntity);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.Detach(eventEntity);
            var wonBySameEvent = await _db.CharacterStateEvents.AsNoTracking().AnyAsync(x =>
                x.UserId == userId && x.WorkId == stateEvent.WorkId && x.CharacterId == stateEvent.CharacterId &&
                x.SourceRunId == stateEvent.SourceRunId && x.SourceEventKey == stateEvent.SourceEventKey,
                cancellationToken);
            if (wonBySameEvent)
                return true;
            throw;
        }
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
            CreateAt = DateTime.UtcNow,
            UpdateBy = userId,
            UpdateAt = DateTime.UtcNow
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

    private static CharacterStateEventEntity CreateEventEntity(
        CharacterStateEventData stateEvent,
        string userId,
        string eventId,
        DateTime now)
        => new()
        {
            Id = eventId,
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

    private static void ApplySnapshot(
        CharacterStateSnapshotEntity entity,
        CharacterStateSnapshotData snapshot,
        string eventId,
        string userId,
        DateTime now)
    {
        entity.BasedOnEventId = eventId;
        entity.StateJson = snapshot.StateJson ?? string.Empty;
        entity.Version = snapshot.Version;
        entity.Status = snapshot.Status ?? "confirmed";
        entity.UpdateBy = userId;
        entity.UpdateAt = now;
    }

    private string ResolveUserId(string userId)
        => string.IsNullOrWhiteSpace(userId) ? _userContext.UserId : userId;
}
