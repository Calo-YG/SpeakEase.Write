using AINWZ.Domain.Entities.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AINWZ.Infrastructure.Persistence.Configurations.Learning;

internal sealed class InspirationRecordEntityConfiguration : IEntityTypeConfiguration<InspirationRecordEntity>
{
    public void Configure(EntityTypeBuilder<InspirationRecordEntity> builder)
    {
        builder.ToTable("inspiration_records");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64);
        builder.Property(x => x.InspirationType).HasMaxLength(64);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Content).HasColumnType("text");
        builder.Property(x => x.Source).HasMaxLength(64);
    }
}
