using AINWZ.Domain.Entities.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AINWZ.Infrastructure.Persistence.Configurations.Learning;

internal sealed class ReferencePassageEntityConfiguration : IEntityTypeConfiguration<ReferencePassageEntity>
{
    public void Configure(EntityTypeBuilder<ReferencePassageEntity> builder)
    {
        builder.ToTable("reference_passages");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.ReferenceWorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PassageType).HasMaxLength(64);
        builder.Property(x => x.Content).HasColumnType("text");
        builder.Property(x => x.HighlightTagsJson).HasColumnType("text");
        builder.Property(x => x.TechniqueAnalysis).HasColumnType("text");
    }
}
