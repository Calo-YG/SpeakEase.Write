using SpeakEase.Write.Domain.Entities.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.AI;

internal sealed class AIModelDefinitionEntityConfiguration : IEntityTypeConfiguration<AIModelDefinitionEntity>
{
    public void Configure(EntityTypeBuilder<AIModelDefinitionEntity> builder)
    {
        builder.ToTable("ai_model_definitions");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.Label).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.ApiBaseUrl).HasMaxLength(512);
        builder.HasIndex(x => x.Provider).IsUnique();
    }
}
