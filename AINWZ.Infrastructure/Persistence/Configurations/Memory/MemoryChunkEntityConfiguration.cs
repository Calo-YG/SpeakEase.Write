using AINWZ.Domain.Entities.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AINWZ.Infrastructure.Persistence.Configurations.Memory;

internal sealed class MemoryChunkEntityConfiguration : IEntityTypeConfiguration<MemoryChunkEntity>
{
    public void Configure(EntityTypeBuilder<MemoryChunkEntity> builder)
    {
        builder.ToTable("memory_chunks");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ChapterId).HasMaxLength(64);
        builder.Property(x => x.MemoryType).HasMaxLength(64);
        builder.Property(x => x.Content).HasColumnType("text");
        builder.Property(x => x.Source).HasMaxLength(64);
        builder.Property(x => x.VersionId).HasMaxLength(64);
        builder.Property(x => x.ModelId).HasMaxLength(64);
        builder.Property(x => x.Metadata).HasColumnType("text");
        builder.Property(x => x.Metadata).ConfigureStringDictionaryProperty<MemoryChunkEntity>();
    }
}
