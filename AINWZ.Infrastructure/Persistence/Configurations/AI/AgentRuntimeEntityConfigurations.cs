using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Infrastructure.Persistence.Configurations;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.AI;

internal sealed class AgentRunEntityConfiguration : IEntityTypeConfiguration<AgentRunEntity>
{
    public void Configure(EntityTypeBuilder<AgentRunEntity> builder)
    {
        builder.ToTable("ai_agent_runs");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SessionId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DeduplicationKey).HasMaxLength(160).IsRequired();
        builder.Property(x => x.ClientMessageId).HasMaxLength(128);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.StopReason).HasMaxLength(64);
        builder.Property(x => x.Content).HasColumnType("text");
        builder.Property(x => x.ResultJson).HasColumnType("text");
        builder.Property(x => x.Model).HasMaxLength(128);
        builder.HasIndex(x => new { x.UserId, x.WorkId, x.SessionId, x.DeduplicationKey }).IsUnique();
        builder.HasIndex(x => new { x.SessionId, x.StartedAt });
    }
}

internal sealed class AgentRunEventEntityConfiguration : IEntityTypeConfiguration<AgentRunEventEntity>
{
    public void Configure(EntityTypeBuilder<AgentRunEventEntity> builder)
    {
        builder.ToTable("ai_agent_run_events");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RunId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.StepId).HasMaxLength(64);
        builder.Property(x => x.Type).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("text");
        builder.HasIndex(x => new { x.RunId, x.Sequence }).IsUnique();
    }
}

internal sealed class AgentToolCallEntityConfiguration : IEntityTypeConfiguration<AgentToolCallEntity>
{
    public void Configure(EntityTypeBuilder<AgentToolCallEntity> builder)
    {
        builder.ToTable("ai_agent_tool_calls");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RunId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.StepId).HasMaxLength(64);
        builder.Property(x => x.ToolCallId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ToolName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ArgumentsHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ResultJson).HasColumnType("text");
        builder.HasIndex(x => new { x.RunId, x.ToolCallId }).IsUnique();
    }
}

internal sealed class AgentArtifactEntityConfiguration : IEntityTypeConfiguration<AgentArtifactEntity>
{
    public void Configure(EntityTypeBuilder<AgentArtifactEntity> builder)
    {
        builder.ToTable("ai_agent_artifacts");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RunId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.StepId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Summary).HasColumnType("text");
        builder.Property(x => x.Content).HasColumnType("text");
        builder.HasIndex(x => new { x.RunId, x.StepId }).IsUnique();
    }
}
