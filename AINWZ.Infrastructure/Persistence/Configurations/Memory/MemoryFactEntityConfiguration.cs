using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpeakEase.Write.Domain.Entities.Memory;
using SpeakEase.Write.Infrastructure.Persistence.Configurations;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Memory;

internal sealed class MemoryFactEntityConfiguration : IEntityTypeConfiguration<MemoryFactEntity>
{
    public void Configure(EntityTypeBuilder<MemoryFactEntity> builder)
    {
        builder.ToTable("memory_facts");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SessionId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Value).HasColumnType("text");
        builder.HasIndex(x => new
        {
            x.UserId,
            x.WorkId,
            x.SessionId,
            x.MemoryGeneration,
            x.Category,
            x.Key
        }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.WorkId, x.IsCurrent });
    }
}
