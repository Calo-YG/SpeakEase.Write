using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeakEase.Write.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IsolateMemoryFactRefreshGenerations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memory_facts_UserId_WorkId_SessionId_Category_Key",
                table: "memory_facts");

            migrationBuilder.AddColumn<long>(
                name: "MemoryGeneration",
                table: "memory_facts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_memory_facts_UserId_WorkId_SessionId_MemoryGeneration_Categ~",
                table: "memory_facts",
                columns: new[] { "UserId", "WorkId", "SessionId", "MemoryGeneration", "Category", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memory_facts_UserId_WorkId_SessionId_MemoryGeneration_Categ~",
                table: "memory_facts");

            migrationBuilder.DropColumn(
                name: "MemoryGeneration",
                table: "memory_facts");

            migrationBuilder.CreateIndex(
                name: "IX_memory_facts_UserId_WorkId_SessionId_Category_Key",
                table: "memory_facts",
                columns: new[] { "UserId", "WorkId", "SessionId", "Category", "Key" },
                unique: true);
        }
    }
}
