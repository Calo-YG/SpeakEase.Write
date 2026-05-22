using SpeakEase.Write.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Users;

internal sealed class UserEntityConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("users");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.Account).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.Account).IsUnique();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.NickName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Salt).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Password).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Avatar).HasMaxLength(512);
        builder.Property(x => x.SubscriptionPlan).HasMaxLength(64);
        builder.Property(x => x.Role).HasMaxLength(32);
    }
}
