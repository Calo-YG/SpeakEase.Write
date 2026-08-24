using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeakEase.Write.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentRuntimeAndMemoryFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_agent_artifacts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RunId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StepId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: true),
                    EstimatedTokens = table.Column<int>(type: "integer", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_agent_artifacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_agent_run_events",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RunId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StepId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_agent_run_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_agent_runs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SessionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeduplicationKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ClientMessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StopReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Content = table.Column<string>(type: "text", nullable: true),
                    ResultJson = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TurnNumber = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_agent_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_agent_tool_calls",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RunId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StepId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ToolCallId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ToolName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ArgumentsHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_agent_tool_calls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "memory_facts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SessionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true),
                    SourceTurn = table.Column<int>(type: "integer", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    VersionTurn = table.Column<int>(type: "integer", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_facts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_artifacts_RunId_StepId",
                table: "ai_agent_artifacts",
                columns: new[] { "RunId", "StepId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_run_events_RunId_Sequence",
                table: "ai_agent_run_events",
                columns: new[] { "RunId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_runs_SessionId_StartedAt",
                table: "ai_agent_runs",
                columns: new[] { "SessionId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_runs_UserId_WorkId_SessionId_DeduplicationKey",
                table: "ai_agent_runs",
                columns: new[] { "UserId", "WorkId", "SessionId", "DeduplicationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_tool_calls_RunId_ToolCallId",
                table: "ai_agent_tool_calls",
                columns: new[] { "RunId", "ToolCallId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_memory_facts_UserId_WorkId_IsCurrent",
                table: "memory_facts",
                columns: new[] { "UserId", "WorkId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_memory_facts_UserId_WorkId_SessionId_Category_Key",
                table: "memory_facts",
                columns: new[] { "UserId", "WorkId", "SessionId", "Category", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_agent_artifacts");

            migrationBuilder.DropTable(
                name: "ai_agent_run_events");

            migrationBuilder.DropTable(
                name: "ai_agent_runs");

            migrationBuilder.DropTable(
                name: "ai_agent_tool_calls");

            migrationBuilder.DropTable(
                name: "memory_facts");
        }
    }
}
