using SpeakEase.Write.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Users;

internal sealed class UserAiModelConfigEntityConfiguration : IEntityTypeConfiguration<UserAiModelConfigEntity>
{
    public void Configure(EntityTypeBuilder<UserAiModelConfigEntity> builder)
    {
        builder.ToTable("user_ai_model_configs");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ConfigName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ProviderId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ModelName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.FallbackProviderId).HasMaxLength(64);
        builder.Property(x => x.FallbackModelName).HasMaxLength(128);
        builder.Property(x => x.Preference).HasMaxLength(128);
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.CapabilityTags).HasColumnType("jsonb");

        // 同一用户同一配置名唯一
        builder.HasIndex(x => new { x.UserId, x.ConfigName }).IsUnique();
        // 快速查询激活配置
        builder.HasIndex(x => new { x.UserId, x.IsActive });
    }
}
