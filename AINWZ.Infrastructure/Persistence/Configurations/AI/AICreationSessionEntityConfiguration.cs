using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Infrastructure.Persistence.Configurations;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.AI;

internal sealed class AICreationSessionEntityConfiguration : IEntityTypeConfiguration<AICreationSessionEntity>
{
    public void Configure(EntityTypeBuilder<AICreationSessionEntity> builder)
    {
        builder.ToTable("ai_creation_sessions");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.AdoptedContentJson).HasColumnType("text");
        builder.Property(x => x.CloseReason).HasMaxLength(256);
        // PostgreSQL 的系统列 xmin 作为乐观并发令牌，防止轮次更新丢失。
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
        builder.HasIndex(x => new { x.WorkId, x.Status });
        builder.HasIndex(x => x.WorkId)
            .IsUnique()
            .HasFilter("\"Status\" = 'active'");
        builder.HasIndex(x => x.LastActivityAt);
    }
}
