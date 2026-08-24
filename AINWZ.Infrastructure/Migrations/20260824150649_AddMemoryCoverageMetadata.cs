using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeakEase.Write.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemoryCoverageMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CoveredFromTurn",
                table: "memory_snapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CoveredToTurn",
                table: "memory_snapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MemoryStatus",
                table: "memory_snapshots",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "fresh");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoveredFromTurn",
                table: "memory_snapshots");

            migrationBuilder.DropColumn(
                name: "CoveredToTurn",
                table: "memory_snapshots");

            migrationBuilder.DropColumn(
                name: "MemoryStatus",
                table: "memory_snapshots");
        }
    }
}
