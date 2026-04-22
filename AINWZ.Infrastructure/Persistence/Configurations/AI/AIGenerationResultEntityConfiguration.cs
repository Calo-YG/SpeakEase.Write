using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpeakEase.Write.Domain.Entities.AI;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.AI;

internal sealed class AIGenerationResultEntityConfiguration : IEntityTypeConfiguration<AIGenerationResultEntity>
{
    public void Configure(EntityTypeBuilder<AIGenerationResultEntity> builder)
    {
        builder.ToTable("ai_generation_results");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.TaskId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ModelId).HasMaxLength(64);
        builder.Property(x => x.Content).HasColumnType("text");
        builder.Property(x => x.Summary).HasColumnType("text");
        builder.Property(x => x.FeedbackStatus).HasMaxLength(64);
        builder.Property(x => x.Keywords).HasColumnType("text");
        builder.Property(x => x.Keywords).ConfigureStringListProperty<AIGenerationResultEntity>();
        builder.Property(x => x.ConfidenceScore).HasPrecision(18, 4);
    }
}
