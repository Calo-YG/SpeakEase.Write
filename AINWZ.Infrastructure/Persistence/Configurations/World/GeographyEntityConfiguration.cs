using SpeakEase.Write.Domain.Entities.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.World;

internal sealed class GeographyEntityConfiguration : IEntityTypeConfiguration<GeographyEntity>
{
    public void Configure(EntityTypeBuilder<GeographyEntity> builder)
    {
        builder.ToTable("geographies");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.WorldSettingId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.GeographyType).HasMaxLength(64);
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.ParentGeographyId).HasMaxLength(64);
    }
}
