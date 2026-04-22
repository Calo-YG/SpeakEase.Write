using SpeakEase.Write.Domain.Entities.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.World;

internal sealed class WorldSettingEntityConfiguration : IEntityTypeConfiguration<WorldSettingEntity>
{
    public void Configure(EntityTypeBuilder<WorldSettingEntity> builder)
    {
        builder.ToTable("world_settings");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorldName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EraBackground).HasColumnType("text");
        builder.Property(x => x.OverallStyle).HasMaxLength(100);
        builder.Property(x => x.Summary).HasColumnType("text");
        builder.Property(x => x.JsonContent).HasColumnType("text");
    }
}
