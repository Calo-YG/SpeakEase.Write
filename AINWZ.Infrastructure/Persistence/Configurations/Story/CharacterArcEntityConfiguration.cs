using AINWZ.Domain.Entities.Story;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AINWZ.Infrastructure.Persistence.Configurations.Story;

internal sealed class CharacterArcEntityConfiguration : IEntityTypeConfiguration<CharacterArcEntity>
{
    public void Configure(EntityTypeBuilder<CharacterArcEntity> builder)
    {
        builder.ToTable("character_arcs");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CharacterId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.StageTitle).HasMaxLength(200).IsRequired();
        builder.Property(x => x.InitialState).HasColumnType("text");
        builder.Property(x => x.ChangedState).HasColumnType("text");
        builder.Property(x => x.TriggerEvent).HasColumnType("text");
    }
}
