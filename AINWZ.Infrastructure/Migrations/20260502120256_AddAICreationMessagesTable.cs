using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeakEase.Write.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAICreationMessagesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContextSnapshotJson",
                table: "ai_creation_sessions");

            migrationBuilder.DropColumn(
                name: "MessagesJson",
                table: "ai_creation_sessions");

            migrationBuilder.CreateTable(
                name: "ai_creation_messages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SessionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    TurnNumber = table.Column<int>(type: "integer", nullable: false),
                    ToolName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ToolSuccess = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_creation_messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_volumes_WorkId_Sequence",
                table: "volumes",
                columns: new[] { "WorkId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_chapters_VolumeId",
                table: "chapters",
                column: "VolumeId");

            migrationBuilder.CreateIndex(
                name: "IX_chapters_WorkId_Sequence",
                table: "chapters",
                columns: new[] { "WorkId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_creation_messages_SessionId_CreatedAt",
                table: "ai_creation_messages",
                columns: new[] { "SessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_creation_messages_SessionId_TurnNumber",
                table: "ai_creation_messages",
                columns: new[] { "SessionId", "TurnNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_creation_messages");

            migrationBuilder.DropIndex(
                name: "IX_volumes_WorkId_Sequence",
                table: "volumes");

            migrationBuilder.DropIndex(
                name: "IX_chapters_VolumeId",
                table: "chapters");

            migrationBuilder.DropIndex(
                name: "IX_chapters_WorkId_Sequence",
                table: "chapters");

            migrationBuilder.AddColumn<string>(
                name: "ContextSnapshotJson",
                table: "ai_creation_sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessagesJson",
                table: "ai_creation_sessions",
                type: "text",
                nullable: true);
        }
    }
}
