using AINWZ.Domain.Entities.Story;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AINWZ.Infrastructure.Persistence.Configurations.Story;

internal sealed class OutlineEntityConfiguration : IEntityTypeConfiguration<OutlineEntity>
{
    public void Configure(EntityTypeBuilder<OutlineEntity> builder)
    {
        builder.ToTable("outlines");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.StructureTemplate).HasMaxLength(64);
        builder.Property(x => x.Summary).HasColumnType("text");
        builder.Property(x => x.JsonContent).HasColumnType("text");
    }
}
