using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeakEase.Write.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsureSingleActiveCreationSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A partial unique index cannot be created while legacy data contains
            // more than one active session for the same work. Keep the newest
            // session active and pause older duplicates so users can resume them.
            migrationBuilder.Sql(
                """
                WITH ranked_active_sessions AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "WorkId"
                               ORDER BY "LastActivityAt" DESC, "UpdateAt" DESC, "Id" DESC) AS row_number
                    FROM ai_creation_sessions
                    WHERE "Status" = 'active'
                )
                UPDATE ai_creation_sessions AS sessions
                SET "Status" = 'paused',
                    "CloseReason" = 'Paused duplicate active session before enforcing one active session per work',
                    "UpdateAt" = CURRENT_TIMESTAMP
                FROM ranked_active_sessions AS ranked
                WHERE sessions."Id" = ranked."Id"
                  AND ranked.row_number > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ai_creation_sessions_WorkId",
                table: "ai_creation_sessions",
                column: "WorkId",
                unique: true,
                filter: "\"Status\" = 'active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ai_creation_sessions_WorkId",
                table: "ai_creation_sessions");
        }
    }
}
