using SpeakEase.Write.Domain.Entities.Story;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Story;

internal sealed class CharacterGraphNodeEntityConfiguration : IEntityTypeConfiguration<CharacterGraphNodeEntity>
{
    public void Configure(EntityTypeBuilder<CharacterGraphNodeEntity> builder)
    {
        builder.ToTable("character_graph_nodes");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.GraphId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CharacterId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NodeType).HasMaxLength(64);
        builder.Property(x => x.X).HasPrecision(18, 4);
        builder.Property(x => x.Y).HasPrecision(18, 4);
        builder.Property(x => x.StyleJson).HasColumnType("text");
        builder.Property(x => x.Metadata).HasColumnType("text");
        builder.Property(x => x.Metadata).ConfigureStringDictionaryProperty<CharacterGraphNodeEntity>();
        builder.HasIndex(x => new { x.WorkId, x.GraphId, x.CharacterId }).IsUnique();
    }
}
