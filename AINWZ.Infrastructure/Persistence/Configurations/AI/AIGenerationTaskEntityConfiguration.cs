using AINWZ.Domain.Entities.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AINWZ.Infrastructure.Persistence.Configurations.AI;

internal sealed class AIGenerationTaskEntityConfiguration : IEntityTypeConfiguration<AIGenerationTaskEntity>
{
    public void Configure(EntityTypeBuilder<AIGenerationTaskEntity> builder)
    {
        builder.ToTable("ai_generation_tasks");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ChapterId).HasMaxLength(64);
        builder.Property(x => x.TaskType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Prompt).HasColumnType("text");
        builder.Property(x => x.ContextSnapshotId).HasMaxLength(64);
        builder.Property(x => x.PrimaryModelId).HasMaxLength(64);
        builder.Property(x => x.FallbackModelId).HasMaxLength(64);
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.ParameterJson).HasColumnType("text");
        builder.Property(x => x.ResultJson).HasColumnType("text");
    }
}
