using SpeakEase.Write.Domain.Entities.Works;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Works;

internal sealed class VolumeEntityConfiguration : IEntityTypeConfiguration<VolumeEntity>
{
    public void Configure(EntityTypeBuilder<VolumeEntity> builder)
    {
        builder.ToTable("volumes");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Summary).HasColumnType("text");
        builder.HasIndex(x => new { x.WorkId, x.Sequence });
    }
}
