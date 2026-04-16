using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AINWZ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMemoryChunkAddAuthorNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "memory_chunks");

            migrationBuilder.AddColumn<string>(
                name: "AuthorNotes",
                table: "chapters",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorNotes",
                table: "chapters");

            migrationBuilder.CreateTable(
                name: "memory_chunks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChapterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Content = table.Column<string>(type: "text", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    MemoryType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    ModelId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VersionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_chunks", x => x.Id);
                });
        }
    }
}
