using AINWZ.Domain.Entities.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AINWZ.Infrastructure.Persistence.Configurations.World;

internal sealed class FactionEntityConfiguration : IEntityTypeConfiguration<FactionEntity>
{
    public void Configure(EntityTypeBuilder<FactionEntity> builder)
    {
        builder.ToTable("factions");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.WorldSettingId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.FactionType).HasMaxLength(64);
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.RelationshipJson).HasColumnType("text");
    }
}
