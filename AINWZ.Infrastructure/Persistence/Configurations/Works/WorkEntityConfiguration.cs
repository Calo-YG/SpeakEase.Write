using SpeakEase.Write.Domain.Entities.Works;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Works;

internal sealed class WorkEntityConfiguration : IEntityTypeConfiguration<WorkEntity>
{
    public void Configure(EntityTypeBuilder<WorkEntity> builder)
    {
        builder.ToTable("works");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Summary).HasColumnType("text");
        builder.Property(x => x.Genre).HasMaxLength(64);
        builder.Property(x => x.StyleTags).HasColumnType("text");
        builder.Property(x => x.StyleTags).ConfigureStringListProperty<WorkEntity>();
        builder.Property(x => x.CreationMode).HasMaxLength(32);
        builder.Property(x => x.Status).HasMaxLength(32);
    }
}
