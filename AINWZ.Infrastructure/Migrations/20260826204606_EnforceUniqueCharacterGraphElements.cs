using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeakEase.Write.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueCharacterGraphElements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE "__character_graph_node_duplicates" ON COMMIT DROP AS
                SELECT "Id" AS "DuplicateId", "CanonicalId", "WorkId", "GraphId"
                FROM (
                    SELECT
                        "Id",
                        "WorkId",
                        "GraphId",
                        FIRST_VALUE("Id") OVER (
                            PARTITION BY "WorkId", "GraphId", "CharacterId"
                            ORDER BY "UpdateAt" DESC, "CreateAt" DESC, "Id"
                        ) AS "CanonicalId"
                    FROM "character_graph_nodes"
                ) AS ranked_nodes
                WHERE "Id" <> "CanonicalId";

                UPDATE "character_graph_edges" AS edge
                SET "SourceNodeId" = duplicate."CanonicalId"
                FROM "__character_graph_node_duplicates" AS duplicate
                WHERE edge."WorkId" = duplicate."WorkId"
                  AND edge."GraphId" = duplicate."GraphId"
                  AND edge."SourceNodeId" = duplicate."DuplicateId";

                UPDATE "character_graph_edges" AS edge
                SET "TargetNodeId" = duplicate."CanonicalId"
                FROM "__character_graph_node_duplicates" AS duplicate
                WHERE edge."WorkId" = duplicate."WorkId"
                  AND edge."GraphId" = duplicate."GraphId"
                  AND edge."TargetNodeId" = duplicate."DuplicateId";

                CREATE TEMP TABLE "__character_graph_edge_duplicates" ON COMMIT DROP AS
                SELECT "Id" AS "DuplicateId"
                FROM (
                    SELECT
                        "Id",
                        FIRST_VALUE("Id") OVER (
                            PARTITION BY "WorkId", "GraphId", "SourceNodeId", "TargetNodeId"
                            ORDER BY "UpdateAt" DESC, "CreateAt" DESC, "Id"
                        ) AS "CanonicalId"
                    FROM "character_graph_edges"
                ) AS ranked_edges
                WHERE "Id" <> "CanonicalId";

                DELETE FROM "character_graph_edges" AS edge
                USING "__character_graph_edge_duplicates" AS duplicate
                WHERE edge."Id" = duplicate."DuplicateId";

                DELETE FROM "character_graph_nodes" AS node
                USING "__character_graph_node_duplicates" AS duplicate
                WHERE node."Id" = duplicate."DuplicateId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_character_graph_nodes_WorkId_GraphId_CharacterId",
                table: "character_graph_nodes",
                columns: new[] { "WorkId", "GraphId", "CharacterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_character_graph_edges_WorkId_GraphId_SourceNodeId_TargetNod~",
                table: "character_graph_edges",
                columns: new[] { "WorkId", "GraphId", "SourceNodeId", "TargetNodeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_character_graph_nodes_WorkId_GraphId_CharacterId",
                table: "character_graph_nodes");

            migrationBuilder.DropIndex(
                name: "IX_character_graph_edges_WorkId_GraphId_SourceNodeId_TargetNod~",
                table: "character_graph_edges");
        }
    }
}
