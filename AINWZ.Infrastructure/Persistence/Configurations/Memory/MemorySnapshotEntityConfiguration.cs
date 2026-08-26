using SpeakEase.Write.Domain.Entities.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Memory;

internal sealed class MemorySnapshotEntityConfiguration : IEntityTypeConfiguration<MemorySnapshotEntity>
{
    public void Configure(EntityTypeBuilder<MemorySnapshotEntity> builder)
    {
        builder.ToTable("memory_snapshots");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SessionId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ChapterId).HasMaxLength(64);
        builder.Property(x => x.SnapshotType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FilePath).HasMaxLength(512);
        builder.Property(x => x.Summary).HasColumnType("text");
        builder.Property(x => x.SnapshotJson).HasColumnType("text");
        builder.Property(x => x.VersionId).HasMaxLength(64);
        builder.Property(x => x.MemoryStatus).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new
            { x.UserId, x.WorkId, x.SessionId, x.SnapshotType, x.MemoryGeneration })
            .IsUnique();
    }
}
