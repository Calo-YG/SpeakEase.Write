using SpeakEase.Write.Domain.Entities.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.World;

internal sealed class WorldRuleEntityConfiguration : IEntityTypeConfiguration<WorldRuleEntity>
{
    public void Configure(EntityTypeBuilder<WorldRuleEntity> builder)
    {
        builder.ToTable("world_rules");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.WorldSettingId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RuleName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RuleType).HasMaxLength(64);
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.ConstraintJson).HasColumnType("text");
    }
}
