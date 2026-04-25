using SpeakEase.Write.Domain.Entities.Tags;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Tags;

internal sealed class TagEntityConfiguration : IEntityTypeConfiguration<TagEntity>
{
    public void Configure(EntityTypeBuilder<TagEntity> builder)
    {
        builder.ToTable("tags");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.Name).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(32);
        builder.Property(x => x.Color).HasMaxLength(32);
        builder.Property(x => x.Description).HasColumnType("text");
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
