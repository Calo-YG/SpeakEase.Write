using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Infrastructure.Persistence.Configurations;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.AI;

internal sealed class AICreationMessageEntityConfiguration : IEntityTypeConfiguration<AICreationMessageEntity>
{
    public void Configure(EntityTypeBuilder<AICreationMessageEntity> builder)
    {
        builder.ToTable("ai_creation_messages");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.SessionId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Role).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Content).HasColumnType("text").IsRequired();
        builder.Property(x => x.ToolName).HasMaxLength(128);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => new { x.SessionId, x.TurnNumber });
        builder.HasIndex(x => new { x.SessionId, x.CreatedAt });
    }
}
