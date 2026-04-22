using SpeakEase.Write.Domain.Entities.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.World;

internal sealed class HistoricalEventEntityConfiguration : IEntityTypeConfiguration<HistoricalEventEntity>
{
    public void Configure(EntityTypeBuilder<HistoricalEventEntity> builder)
    {
        builder.ToTable("historical_events");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.WorldSettingId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.EraLabel).HasMaxLength(100);
        builder.Property(x => x.ImpactSummary).HasColumnType("text");
    }
}
