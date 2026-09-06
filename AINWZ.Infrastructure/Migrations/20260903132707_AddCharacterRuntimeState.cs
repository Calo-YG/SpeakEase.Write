using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeakEase.Write.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterRuntimeState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_growth_proposals",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CharacterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceRunId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProposalJson = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReviewedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_growth_proposals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "character_state_events",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CharacterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceRunId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceChapterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceEventKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EvidenceJson = table.Column<string>(type: "text", nullable: true),
                    ChangesJson = table.Column<string>(type: "text", nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_state_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "character_state_snapshots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CharacterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BasedOnEventId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StateJson = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_state_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "relationship_state_events",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceCharacterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetCharacterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceRunId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceChapterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChangesJson = table.Column<string>(type: "text", nullable: false),
                    EvidenceJson = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_relationship_state_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_character_growth_proposals_UserId_SourceRunId",
                table: "character_growth_proposals",
                columns: new[] { "UserId", "SourceRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_character_growth_proposals_UserId_WorkId_CharacterId_Status",
                table: "character_growth_proposals",
                columns: new[] { "UserId", "WorkId", "CharacterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_character_state_events_UserId_WorkId_CharacterId_SourceRunI~",
                table: "character_state_events",
                columns: new[] { "UserId", "WorkId", "CharacterId", "SourceRunId", "SourceEventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_character_state_events_WorkId_CharacterId_Version",
                table: "character_state_events",
                columns: new[] { "WorkId", "CharacterId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_character_state_snapshots_UserId_WorkId_CharacterId",
                table: "character_state_snapshots",
                columns: new[] { "UserId", "WorkId", "CharacterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_character_state_snapshots_WorkId_CharacterId_Version",
                table: "character_state_snapshots",
                columns: new[] { "WorkId", "CharacterId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_relationship_state_events_UserId_WorkId_SourceCharacterId_T~",
                table: "relationship_state_events",
                columns: new[] { "UserId", "WorkId", "SourceCharacterId", "TargetCharacterId", "SourceRunId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_growth_proposals");

            migrationBuilder.DropTable(
                name: "character_state_events");

            migrationBuilder.DropTable(
                name: "character_state_snapshots");

            migrationBuilder.DropTable(
                name: "relationship_state_events");
        }
    }
}
