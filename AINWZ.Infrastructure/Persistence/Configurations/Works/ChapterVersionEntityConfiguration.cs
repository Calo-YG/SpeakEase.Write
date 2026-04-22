using SpeakEase.Write.Domain.Entities.Works;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Works;

internal sealed class ChapterVersionEntityConfiguration : IEntityTypeConfiguration<ChapterVersionEntity>
{
    public void Configure(EntityTypeBuilder<ChapterVersionEntity> builder)
    {
        builder.ToTable("chapter_versions");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.ChapterId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Content).HasColumnType("text");
        builder.Property(x => x.Summary).HasColumnType("text");
        builder.Property(x => x.Source).HasMaxLength(64);
        builder.Property(x => x.ModelId).HasMaxLength(64);
    }
}
