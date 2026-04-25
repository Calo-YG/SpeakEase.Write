using SpeakEase.Write.Domain.Entities.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Learning;

internal sealed class UserPassageFavoriteConfiguration : IEntityTypeConfiguration<UserPassageFavoriteEntity>
{
    public void Configure(EntityTypeBuilder<UserPassageFavoriteEntity> builder)
    {
        builder.ToTable("user_passage_favorites");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PassageId).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.PassageId }).IsUnique();
    }
}
