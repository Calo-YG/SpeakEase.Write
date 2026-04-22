using SpeakEase.Write.Domain.Entities.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.World;

internal sealed class PowerSystemEntityConfiguration : IEntityTypeConfiguration<PowerSystemEntity>
{
    public void Configure(EntityTypeBuilder<PowerSystemEntity> builder)
    {
        builder.ToTable("power_systems");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.WorldSettingId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LevelDefinitionJson).HasColumnType("text");
        builder.Property(x => x.AbilityRule).HasColumnType("text");
        builder.Property(x => x.ResourceSystem).HasColumnType("text");
    }
}
