using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Persistence.Configurations;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Story;

internal sealed class CharacterStateEventEntityConfiguration : IEntityTypeConfiguration<CharacterStateEventEntity>
{
    public void Configure(EntityTypeBuilder<CharacterStateEventEntity> builder)
    {
        builder.ToTable("character_state_events");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CharacterId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceRunId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceChapterId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceEventKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.EvidenceJson).HasColumnType("text");
        builder.Property(x => x.ChangesJson).HasColumnType("text");
        builder.Property(x => x.Confidence).IsRequired();
        builder.Property(x => x.Version).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.WorkId, x.CharacterId, x.SourceRunId, x.SourceEventKey }).IsUnique();
        builder.HasIndex(x => new { x.WorkId, x.CharacterId, x.Version });
    }
}

internal sealed class CharacterStateSnapshotEntityConfiguration : IEntityTypeConfiguration<CharacterStateSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<CharacterStateSnapshotEntity> builder)
    {
        builder.ToTable("character_state_snapshots");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CharacterId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.BasedOnEventId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.StateJson).HasColumnType("text").IsRequired();
        builder.Property(x => x.Version).IsRequired().IsConcurrencyToken();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.WorkId, x.CharacterId }).IsUnique();
        builder.HasIndex(x => new { x.WorkId, x.CharacterId, x.Version });
    }
}

internal sealed class CharacterGrowthProposalEntityConfiguration : IEntityTypeConfiguration<CharacterGrowthProposalEntity>
{
    public void Configure(EntityTypeBuilder<CharacterGrowthProposalEntity> builder)
    {
        builder.ToTable("character_growth_proposals");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CharacterId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceRunId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ProposalJson).HasColumnType("text").IsRequired();
        builder.Property(x => x.Severity).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ReviewedBy).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.WorkId, x.CharacterId, x.Status });
        builder.HasIndex(x => new { x.UserId, x.SourceRunId });
    }
}

internal sealed class RelationshipStateEventEntityConfiguration : IEntityTypeConfiguration<RelationshipStateEventEntity>
{
    public void Configure(EntityTypeBuilder<RelationshipStateEventEntity> builder)
    {
        builder.ToTable("relationship_state_events");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceCharacterId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TargetCharacterId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceRunId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceChapterId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ChangesJson).HasColumnType("text").IsRequired();
        builder.Property(x => x.EvidenceJson).HasColumnType("text").IsRequired();
        builder.Property(x => x.Confidence).IsRequired();
        builder.Property(x => x.Version).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.WorkId, x.SourceCharacterId, x.TargetCharacterId, x.SourceRunId });
    }
}
