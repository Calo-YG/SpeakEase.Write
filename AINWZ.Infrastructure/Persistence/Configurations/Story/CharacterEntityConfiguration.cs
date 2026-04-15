using AINWZ.Domain.Entities.Story;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AINWZ.Infrastructure.Persistence.Configurations.Story;

internal sealed class CharacterEntityConfiguration : IEntityTypeConfiguration<CharacterEntity>
{
    public void Configure(EntityTypeBuilder<CharacterEntity> builder)
    {
        builder.ToTable("characters");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Alias).HasMaxLength(100);
        builder.Property(x => x.Gender).HasMaxLength(32);
        builder.Property(x => x.AgeDescription).HasMaxLength(64);
        builder.Property(x => x.Identity).HasMaxLength(128);
        builder.Property(x => x.Appearance).HasColumnType("text");
        builder.Property(x => x.Personality).HasColumnType("text");
        builder.Property(x => x.BackgroundStory).HasColumnType("text");
        builder.Property(x => x.Motivation).HasColumnType("text");
        builder.Property(x => x.AbilityDescription).HasColumnType("text");
        builder.Property(x => x.Tags).HasColumnType("text");
        builder.Property(x => x.Tags).ConfigureStringListProperty<CharacterEntity>();
        builder.Property(x => x.Metadata).HasColumnType("text");
        builder.Property(x => x.Metadata).ConfigureStringDictionaryProperty<CharacterEntity>();
    }
}
