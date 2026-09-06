using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeakEase.Write.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenAgentRuntimeConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ai_agent_tool_calls_RunId_ToolCallId",
                table: "ai_agent_tool_calls");

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_tool_calls_RunId_StepId_ToolCallId",
                table: "ai_agent_tool_calls",
                columns: new[] { "RunId", "StepId", "ToolCallId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ai_agent_tool_calls_RunId_StepId_ToolCallId",
                table: "ai_agent_tool_calls");

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_tool_calls_RunId_ToolCallId",
                table: "ai_agent_tool_calls",
                columns: new[] { "RunId", "ToolCallId" },
                unique: true);
        }
    }
}
