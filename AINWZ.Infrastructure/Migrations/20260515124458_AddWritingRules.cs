using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeakEase.Write.Infrastructure.Migrations
{
    public partial class AddWritingRules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WritingRules",
                table: "works",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                "ALTER TABLE historical_events ALTER COLUMN \"EventTime\" TYPE text USING \"EventTime\"::text;");
            migrationBuilder.Sql(
                "ALTER TABLE historical_events ALTER COLUMN \"EventTime\" DROP NOT NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE historical_events ALTER COLUMN \"EventTime\" TYPE timestamp with time zone USING NULLIF(\"EventTime\", '')::timestamptz;");

            migrationBuilder.DropColumn(
                name: "WritingRules",
                table: "works");
        }
    }
}
