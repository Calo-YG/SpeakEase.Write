using AINWZ.Domain.Entities.Works;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AINWZ.Infrastructure.Persistence.Configurations.Works;

internal sealed class ChapterEntityConfiguration : IEntityTypeConfiguration<ChapterEntity>
{
    public void Configure(EntityTypeBuilder<ChapterEntity> builder)
    {
        builder.ToTable("chapters");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.VolumeId).HasMaxLength(64);
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Content).HasColumnType("text");
        builder.Property(x => x.Summary).HasColumnType("text");
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.OutlineNodeIds).HasColumnType("text");
        builder.Property(x => x.OutlineNodeIds).ConfigureStringListProperty<ChapterEntity>();
    }
}
