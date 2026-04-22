using SpeakEase.Write.Domain.Entities.Story;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Story;

internal sealed class ForeshadowingEntityConfiguration : IEntityTypeConfiguration<ForeshadowingEntity>
{
    public void Configure(EntityTypeBuilder<ForeshadowingEntity> builder)
    {
        builder.ToTable("foreshadowings");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.SetupChapterId).HasMaxLength(64);
        builder.Property(x => x.PayoffChapterId).HasMaxLength(64);
        builder.Property(x => x.Status).HasMaxLength(32);
    }
}
