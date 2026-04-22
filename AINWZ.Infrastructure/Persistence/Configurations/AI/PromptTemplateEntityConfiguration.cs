using SpeakEase.Write.Domain.Entities.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.AI;

internal sealed class PromptTemplateEntityConfiguration : IEntityTypeConfiguration<PromptTemplateEntity>
{
    public void Configure(EntityTypeBuilder<PromptTemplateEntity> builder)
    {
        builder.ToTable("prompt_templates");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Scenario).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Content).HasColumnType("text");
        builder.Property(x => x.Version).HasMaxLength(32);
        builder.Property(x => x.Variables).HasColumnType("text");
        builder.Property(x => x.Variables).ConfigureStringDictionaryProperty<PromptTemplateEntity>();
    }
}
