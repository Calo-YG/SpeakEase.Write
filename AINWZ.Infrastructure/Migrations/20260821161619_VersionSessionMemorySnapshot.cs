using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeakEase.Write.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VersionSessionMemorySnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memory_snapshots_UserId_WorkId_SessionId_SnapshotType",
                table: "memory_snapshots");

            // 旧版本每轮追加快照，创建唯一索引前保留每个作用域最新版本，避免升级因历史重复数据失败。
            migrationBuilder.Sql(@"
                DELETE FROM memory_snapshots older
                USING memory_snapshots newer
                WHERE older.""UserId"" = newer.""UserId""
                  AND older.""WorkId"" = newer.""WorkId""
                  AND older.""SessionId"" = newer.""SessionId""
                  AND older.""SnapshotType"" = newer.""SnapshotType""
                  AND (
                      CASE WHEN older.""VersionId"" ~ '^[0-9]+$' THEN older.""VersionId""::bigint ELSE 0 END,
                      older.""CreateAt"",
                      older.""Id""
                  ) < (
                      CASE WHEN newer.""VersionId"" ~ '^[0-9]+$' THEN newer.""VersionId""::bigint ELSE 0 END,
                      newer.""CreateAt"",
                      newer.""Id""
                  );");

            migrationBuilder.CreateIndex(
                name: "IX_memory_snapshots_UserId_WorkId_SessionId_SnapshotType",
                table: "memory_snapshots",
                columns: new[] { "UserId", "WorkId", "SessionId", "SnapshotType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memory_snapshots_UserId_WorkId_SessionId_SnapshotType",
                table: "memory_snapshots");

            migrationBuilder.CreateIndex(
                name: "IX_memory_snapshots_UserId_WorkId_SessionId_SnapshotType",
                table: "memory_snapshots",
                columns: new[] { "UserId", "WorkId", "SessionId", "SnapshotType" });
        }
    }
}
