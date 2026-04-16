using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AINWZ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChapterAnalysisAndExtendEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkId",
                table: "world_rules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkId",
                table: "power_systems",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "llm_call_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkId",
                table: "historical_events",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkId",
                table: "geographies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkId",
                table: "factions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContextMode",
                table: "context_assembly_logs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoreSettingTokens",
                table: "context_assembly_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecentContextTokens",
                table: "context_assembly_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RetrievedContextTokens",
                table: "context_assembly_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotId",
                table: "context_assembly_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastContentSavedAt",
                table: "chapters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultJson",
                table: "ai_generation_tasks",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "chapter_analysis_results",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TaskId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChapterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AnalysisType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    CreatedEntityIds = table.Column<string>(type: "jsonb", nullable: true),
                    IsConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    UserFeedback = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chapter_analysis_results", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chapter_analysis_results_ChapterId",
                table: "chapter_analysis_results",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_chapter_analysis_results_TaskId",
                table: "chapter_analysis_results",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_chapter_analysis_results_WorkId",
                table: "chapter_analysis_results",
                column: "WorkId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chapter_analysis_results");

            migrationBuilder.DropColumn(
                name: "WorkId",
                table: "world_rules");

            migrationBuilder.DropColumn(
                name: "WorkId",
                table: "power_systems");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "llm_call_logs");

            migrationBuilder.DropColumn(
                name: "WorkId",
                table: "historical_events");

            migrationBuilder.DropColumn(
                name: "WorkId",
                table: "geographies");

            migrationBuilder.DropColumn(
                name: "WorkId",
                table: "factions");

            migrationBuilder.DropColumn(
                name: "ContextMode",
                table: "context_assembly_logs");

            migrationBuilder.DropColumn(
                name: "CoreSettingTokens",
                table: "context_assembly_logs");

            migrationBuilder.DropColumn(
                name: "RecentContextTokens",
                table: "context_assembly_logs");

            migrationBuilder.DropColumn(
                name: "RetrievedContextTokens",
                table: "context_assembly_logs");

            migrationBuilder.DropColumn(
                name: "SnapshotId",
                table: "context_assembly_logs");

            migrationBuilder.DropColumn(
                name: "LastContentSavedAt",
                table: "chapters");

            migrationBuilder.DropColumn(
                name: "ResultJson",
                table: "ai_generation_tasks");
        }
    }
}
