using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AINWZ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAiModelConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContextSource",
                table: "user_ai_model_configs");

            migrationBuilder.DropColumn(
                name: "FallbackModelId",
                table: "user_ai_model_configs");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "user_ai_model_configs");

            migrationBuilder.DropColumn(
                name: "PrimaryModelId",
                table: "user_ai_model_configs");

            migrationBuilder.DropColumn(
                name: "ContextWindow",
                table: "ai_model_definitions");

            migrationBuilder.DropColumn(
                name: "EstimateCost",
                table: "ai_model_definitions");

            migrationBuilder.DropColumn(
                name: "LatencyTargetMs",
                table: "ai_model_definitions");

            migrationBuilder.DropColumn(
                name: "MaxOutputTokens",
                table: "ai_model_definitions");

            migrationBuilder.DropColumn(
                name: "SupportsStreaming",
                table: "ai_model_definitions");

            migrationBuilder.RenameColumn(
                name: "VersionId",
                table: "user_ai_model_configs",
                newName: "FallbackProviderId");

            migrationBuilder.RenameColumn(
                name: "ModelWeights",
                table: "user_ai_model_configs",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "ModelGroup",
                table: "user_ai_model_configs",
                newName: "ProviderId");

            migrationBuilder.RenameColumn(
                name: "SupportsToolCall",
                table: "ai_model_definitions",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "CapabilityTags",
                table: "ai_model_definitions",
                newName: "ApiKey");

            migrationBuilder.AddColumn<string>(
                name: "CapabilityTags",
                table: "user_ai_model_configs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfigName",
                table: "user_ai_model_configs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ContextWindow",
                table: "user_ai_model_configs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimateCost",
                table: "user_ai_model_configs",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "FallbackModelName",
                table: "user_ai_model_configs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "user_ai_model_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxOutputTokens",
                table: "user_ai_model_configs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                table: "user_ai_model_configs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "SupportsStreaming",
                table: "user_ai_model_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsToolCall",
                table: "user_ai_model_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ApiBaseUrl",
                table: "ai_model_definitions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_ai_model_configs_UserId_ConfigName",
                table: "user_ai_model_configs",
                columns: new[] { "UserId", "ConfigName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_ai_model_configs_UserId_IsActive",
                table: "user_ai_model_configs",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_model_definitions_Provider",
                table: "ai_model_definitions",
                column: "Provider",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_ai_model_configs_UserId_ConfigName",
                table: "user_ai_model_configs");

            migrationBuilder.DropIndex(
                name: "IX_user_ai_model_configs_UserId_IsActive",
                table: "user_ai_model_configs");

            migrationBuilder.DropIndex(
                name: "IX_ai_model_definitions_Provider",
                table: "ai_model_definitions");

            migrationBuilder.DropColumn(
                name: "CapabilityTags",
                table: "user_ai_model_configs");

            migrationBuilder.DropColumn(
                name: "ConfigName",
                table: "user_ai_model_configs");

            migrationBuilder.DropColumn(
                name: "ContextWindow",
                table: "user_ai_model_configs");

            migrationBuilder.DropColumn(
                name: "EstimateCost",
                table: "user_ai_model_configs");

            migrationBuilder.DropColumn(
                name: "FallbackModelName",
                table: "user_ai_model_configs");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "user_ai_model_configs");

            migrationBuilder.DropColumn(
                name: "MaxOutputTokens",
                table: "user_ai_model_configs");

            migrationBuilder.DropColumn(
                name: "ModelName",
                table: "user_ai_model_configs");

            migrationBuilder.DropColumn(
                name: "SupportsStreaming",
                table: "user_ai_model_configs");

            migrationBuilder.DropColumn(
                name: "SupportsToolCall",
                table: "user_ai_model_configs");

            migrationBuilder.DropColumn(
                name: "ApiBaseUrl",
                table: "ai_model_definitions");

            migrationBuilder.RenameColumn(
                name: "ProviderId",
                table: "user_ai_model_configs",
                newName: "ModelGroup");

            migrationBuilder.RenameColumn(
                name: "FallbackProviderId",
                table: "user_ai_model_configs",
                newName: "VersionId");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "user_ai_model_configs",
                newName: "ModelWeights");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "ai_model_definitions",
                newName: "SupportsToolCall");

            migrationBuilder.RenameColumn(
                name: "ApiKey",
                table: "ai_model_definitions",
                newName: "CapabilityTags");

            migrationBuilder.AddColumn<string>(
                name: "ContextSource",
                table: "user_ai_model_configs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FallbackModelId",
                table: "user_ai_model_configs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "user_ai_model_configs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryModelId",
                table: "user_ai_model_configs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContextWindow",
                table: "ai_model_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimateCost",
                table: "ai_model_definitions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "LatencyTargetMs",
                table: "ai_model_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxOutputTokens",
                table: "ai_model_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsStreaming",
                table: "ai_model_definitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
