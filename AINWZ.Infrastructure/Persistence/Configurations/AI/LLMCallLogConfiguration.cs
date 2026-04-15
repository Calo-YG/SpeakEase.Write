using AINWZ.Domain.Entities.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AINWZ.Infrastructure.Persistence.Configurations.AI;

/// <summary>
/// LLM 调用日志实体映射配置�?/// </summary>
public sealed class LLMCallLogConfiguration : IEntityTypeConfiguration<LLMCallLogEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LLMCallLogEntity> builder)
    {
        builder.ToTable("llm_call_logs");
        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Id).HasMaxLength(64);
        builder.Property(entity => entity.CallType).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.SkillName).HasMaxLength(64);
        builder.Property(entity => entity.RequestSummary).HasMaxLength(4000).IsRequired();
        builder.Property(entity => entity.ResponseSummary).HasMaxLength(4000);
        builder.Property(entity => entity.PrimaryModel).HasMaxLength(128);
        builder.Property(entity => entity.FinalModel).HasMaxLength(128);
        builder.Property(entity => entity.FallbackModel).HasMaxLength(128);
        builder.Property(entity => entity.RequestId).HasMaxLength(128);
        builder.Property(entity => entity.FinishReason).HasMaxLength(64);
        builder.Property(entity => entity.ToolCallsSummary).HasMaxLength(4000);
        builder.Property(entity => entity.ToolResultsSummary).HasMaxLength(4000);
        builder.Property(entity => entity.ErrorMessage).HasMaxLength(4000);
    }
}
