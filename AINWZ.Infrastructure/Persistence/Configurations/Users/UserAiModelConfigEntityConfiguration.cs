using System.Text.Json;
using AINWZ.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AINWZ.Infrastructure.Persistence.Configurations.Users;

internal sealed class UserAiModelConfigEntityConfiguration : IEntityTypeConfiguration<UserAiModelConfigEntity>
{
    public void Configure(EntityTypeBuilder<UserAiModelConfigEntity> builder)
    {
        builder.ToTable("user_ai_model_configs");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ModelGroup).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PrimaryModelId).HasMaxLength(64);
        builder.Property(x => x.FallbackModelId).HasMaxLength(64);
        builder.Property(x => x.ContextSource).HasMaxLength(64);
        builder.Property(x => x.Preference).HasMaxLength(128);
        builder.Property(x => x.VersionId).HasMaxLength(64);
        builder.Property(x => x.Metadata).HasColumnType("text");
        builder.Property(x => x.Metadata).ConfigureStringDictionaryProperty<UserAiModelConfigEntity>();

        var modelWeightsProperty = builder.Property(x => x.ModelWeights)
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null),
                value => string.IsNullOrWhiteSpace(value)
                    ? new Dictionary<string, decimal>()
                    : JsonSerializer.Deserialize<Dictionary<string, decimal>>(value, (JsonSerializerOptions)null) ?? new Dictionary<string, decimal>());

        modelWeightsProperty.Metadata.SetValueComparer(new ValueComparer<Dictionary<string, decimal>>(
            (left, right) => left != null && right != null && left.OrderBy(item => item.Key).SequenceEqual(right.OrderBy(item => item.Key)),
            value => value.OrderBy(item => item.Key).Aggregate(0, (hash, item) => HashCode.Combine(hash, item.Key, item.Value)),
            value => value.ToDictionary(item => item.Key, item => item.Value)));
    }
}
