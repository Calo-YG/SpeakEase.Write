using SpeakEase.Write.Domain.Entities.Story;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Story;

internal sealed class TimelineEventEntityConfiguration : IEntityTypeConfiguration<TimelineEventEntity>
{
    public void Configure(EntityTypeBuilder<TimelineEventEntity> builder)
    {
        builder.ToTable("timeline_events");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ChapterId).HasMaxLength(64);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.EventType).HasMaxLength(64);
        builder.Property(x => x.RelatedCharacterIds).HasColumnType("text");
        builder.Property(x => x.RelatedCharacterIds).ConfigureStringListProperty<TimelineEventEntity>();
    }
}
