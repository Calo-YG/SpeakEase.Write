using AINWZ.Domain.Entities.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AINWZ.Infrastructure.Persistence.Configurations.AI;

internal sealed class ChapterAnalysisResultEntityConfiguration : IEntityTypeConfiguration<ChapterAnalysisResultEntity>
{
    public void Configure(EntityTypeBuilder<ChapterAnalysisResultEntity> builder)
    {
        builder.ToTable("chapter_analysis_results");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.TaskId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ChapterId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.AnalysisType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResultJson).HasColumnType("text");
        builder.Property(x => x.Summary).HasColumnType("text");
        builder.Property(x => x.CreatedEntityIds).HasColumnType("jsonb");
        builder.Property(x => x.UserFeedback).HasMaxLength(32);

        builder.HasIndex(x => x.WorkId);
        builder.HasIndex(x => x.ChapterId);
        builder.HasIndex(x => x.TaskId);
    }
}
