using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeakEase.Write.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionMemoryScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "memory_snapshots",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "context_assembly_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_users_Account",
                table: "users",
                column: "Account",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_memory_snapshots_UserId_WorkId_SessionId_SnapshotType",
                table: "memory_snapshots",
                columns: new[] { "UserId", "WorkId", "SessionId", "SnapshotType" });

            migrationBuilder.CreateIndex(
                name: "IX_context_assembly_logs_UserId_WorkId_SessionId_ContextMode",
                table: "context_assembly_logs",
                columns: new[] { "UserId", "WorkId", "SessionId", "ContextMode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_Account",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_Email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_memory_snapshots_UserId_WorkId_SessionId_SnapshotType",
                table: "memory_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_context_assembly_logs_UserId_WorkId_SessionId_ContextMode",
                table: "context_assembly_logs");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "memory_snapshots");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "context_assembly_logs");
        }
    }
}
