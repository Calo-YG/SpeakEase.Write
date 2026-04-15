using AINWZ.Domain.Entities.Story;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AINWZ.Infrastructure.Persistence.Configurations.Story;

internal sealed class OutlineNodeEntityConfiguration : IEntityTypeConfiguration<OutlineNodeEntity>
{
    public void Configure(EntityTypeBuilder<OutlineNodeEntity> builder)
    {
        builder.ToTable("outline_nodes");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.OutlineId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ParentNodeId).HasMaxLength(64);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Goal).HasColumnType("text");
        builder.Property(x => x.KeyEvent).HasColumnType("text");
        builder.Property(x => x.StageType).HasMaxLength(64);
        builder.Property(x => x.CharacterIds).HasColumnType("text");
        builder.Property(x => x.CharacterIds).ConfigureStringListProperty<OutlineNodeEntity>();
    }
}
