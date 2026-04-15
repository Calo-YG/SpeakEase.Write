using AINWZ.Domain.Entities.Story;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AINWZ.Infrastructure.Persistence.Configurations.Story;

internal sealed class CharacterGraphEdgeEntityConfiguration : IEntityTypeConfiguration<CharacterGraphEdgeEntity>
{
    public void Configure(EntityTypeBuilder<CharacterGraphEdgeEntity> builder)
    {
        builder.ToTable("character_graph_edges");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.GraphId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceNodeId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TargetNodeId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RelationshipId).HasMaxLength(64);
        builder.Property(x => x.RelationType).HasMaxLength(64);
        builder.Property(x => x.Label).HasMaxLength(100);
        builder.Property(x => x.Direction).HasMaxLength(32);
        builder.Property(x => x.StyleJson).HasColumnType("text");
        builder.Property(x => x.Metadata).HasColumnType("text");
        builder.Property(x => x.Metadata).ConfigureStringDictionaryProperty<CharacterGraphEdgeEntity>();
    }
}
