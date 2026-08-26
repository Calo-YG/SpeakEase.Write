using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeakEase.Write.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IsolateMemoryRefreshGenerations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memory_snapshots_UserId_WorkId_SessionId_SnapshotType",
                table: "memory_snapshots");

            migrationBuilder.AddColumn<long>(
                name: "MemoryGeneration",
                table: "memory_snapshots",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MemoryGeneration",
                table: "ai_creation_sessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_memory_snapshots_UserId_WorkId_SessionId_SnapshotType_Memor~",
                table: "memory_snapshots",
                columns: new[] { "UserId", "WorkId", "SessionId", "SnapshotType", "MemoryGeneration" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memory_snapshots_UserId_WorkId_SessionId_SnapshotType_Memor~",
                table: "memory_snapshots");

            migrationBuilder.DropColumn(
                name: "MemoryGeneration",
                table: "memory_snapshots");

            migrationBuilder.DropColumn(
                name: "MemoryGeneration",
                table: "ai_creation_sessions");

            migrationBuilder.CreateIndex(
                name: "IX_memory_snapshots_UserId_WorkId_SessionId_SnapshotType",
                table: "memory_snapshots",
                columns: new[] { "UserId", "WorkId", "SessionId", "SnapshotType" },
                unique: true);
        }
    }
}
