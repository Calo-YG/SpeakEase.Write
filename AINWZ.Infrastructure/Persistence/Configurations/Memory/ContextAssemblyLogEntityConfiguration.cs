using SpeakEase.Write.Domain.Entities.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Memory;

internal sealed class ContextAssemblyLogEntityConfiguration : IEntityTypeConfiguration<ContextAssemblyLogEntity>
{
    public void Configure(EntityTypeBuilder<ContextAssemblyLogEntity> builder)
    {
        builder.ToTable("context_assembly_logs");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ChapterId).HasMaxLength(64);
        builder.Property(x => x.TaskId).HasMaxLength(64);
        builder.Property(x => x.ContextMode).HasMaxLength(32);
        builder.Property(x => x.SnapshotId).HasMaxLength(64);
        builder.Property(x => x.PrimaryModelId).HasMaxLength(64);
        builder.Property(x => x.FallbackModelId).HasMaxLength(64);
        builder.Property(x => x.SelectedChunkIdsJson).HasColumnType("text");
        builder.Property(x => x.AssemblySummary).HasColumnType("text");
    }
}
