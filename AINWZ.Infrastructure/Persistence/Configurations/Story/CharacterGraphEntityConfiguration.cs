using SpeakEase.Write.Domain.Entities.Story;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Story;

internal sealed class CharacterGraphEntityConfiguration : IEntityTypeConfiguration<CharacterGraphEntity>
{
    public void Configure(EntityTypeBuilder<CharacterGraphEntity> builder)
    {
        builder.ToTable("character_graphs");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.LayoutJson).HasColumnType("text");
        builder.Property(x => x.Metadata).HasColumnType("text");
        builder.Property(x => x.Metadata).ConfigureStringDictionaryProperty<CharacterGraphEntity>();
    }
}
