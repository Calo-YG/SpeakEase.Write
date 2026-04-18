using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AINWZ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentLoopFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Iterations",
                table: "llm_call_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StopReason",
                table: "llm_call_logs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Iterations",
                table: "llm_call_logs");

            migrationBuilder.DropColumn(
                name: "StopReason",
                table: "llm_call_logs");
        }
    }
}
