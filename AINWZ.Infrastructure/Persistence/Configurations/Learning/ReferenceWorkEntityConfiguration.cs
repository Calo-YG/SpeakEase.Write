using SpeakEase.Write.Domain.Entities.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Learning;

internal sealed class ReferenceWorkEntityConfiguration : IEntityTypeConfiguration<ReferenceWorkEntity>
{
    public void Configure(EntityTypeBuilder<ReferenceWorkEntity> builder)
    {
        builder.ToTable("reference_works");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Author).HasMaxLength(100);
        builder.Property(x => x.Genre).HasMaxLength(64);
        builder.Property(x => x.StyleTags).HasColumnType("text");
        builder.Property(x => x.StyleTags).ConfigureStringListProperty<ReferenceWorkEntity>();
        builder.Property(x => x.Summary).HasColumnType("text");
        builder.Property(x => x.Source).HasMaxLength(128);
    }
}
