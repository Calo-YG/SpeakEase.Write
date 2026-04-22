using SpeakEase.Write.Domain.Entities.Story;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Story;

internal sealed class CharacterRelationshipEntityConfiguration : IEntityTypeConfiguration<CharacterRelationshipEntity>
{
    public void Configure(EntityTypeBuilder<CharacterRelationshipEntity> builder)
    {
        builder.ToTable("character_relationships");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceCharacterId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TargetCharacterId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RelationshipType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Description).HasColumnType("text");
    }
}
