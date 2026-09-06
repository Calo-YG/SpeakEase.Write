using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using SpeakEase.Write.Infrastructure.Persistence;

namespace AINWZ.Tests.Infrastructure;

public sealed class MigrationChainTests
{
    [Fact]
    public void FullMigrationScript_CreatesTablesBeforeFirstAlter()
    {
        var script = GenerateScript();

        var createTablePosition = script.IndexOf("CREATE TABLE memory_snapshots", StringComparison.Ordinal);
        var alterTablePosition = script.IndexOf("ALTER TABLE memory_snapshots", StringComparison.Ordinal);

        Assert.True(createTablePosition >= 0, "The migration chain must create memory_snapshots.");
        Assert.True(alterTablePosition >= 0, "The migration chain must later alter memory_snapshots.");
        Assert.True(createTablePosition < alterTablePosition,
            "memory_snapshots must be created before it is altered.");
    }

    [Fact]
    public void SingleActiveSessionMigration_DeduplicatesRowsBeforeCreatingUniqueIndex()
    {
        var script = GenerateScript();

        var deduplicatePosition = script.IndexOf("ROW_NUMBER() OVER", StringComparison.Ordinal);
        var uniqueIndexPosition = script.IndexOf(
            "CREATE UNIQUE INDEX \"IX_ai_creation_sessions_WorkId\"",
            StringComparison.Ordinal);

        Assert.True(deduplicatePosition >= 0,
            "The migration must rank duplicate active sessions before creating the unique index.");
        Assert.True(uniqueIndexPosition >= 0, "The migration must create the partial unique index.");
        Assert.True(deduplicatePosition < uniqueIndexPosition,
            "Duplicate active sessions must be cleaned before the unique index is created.");
    }

    [Fact]
    public void WritingRulesMigration_PreservesNullableTextModel()
    {
        var migrationPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "AINWZ.Infrastructure", "Migrations",
            "20260515124458_AddWritingRules.cs");
        var source = File.ReadAllText(migrationPath);

        Assert.Contains("name: \"WritingRules\"", source);
        Assert.Contains("nullable: true", source);
        Assert.DoesNotContain("defaultValue: \"\"", source);
        Assert.Contains("USING \\\"EventTime\\\"::text", source);
        Assert.Contains("USING NULLIF(\\\"EventTime\\\", '')::timestamptz", source);

        var script = GenerateScript();
        Assert.Contains(
            "ALTER TABLE historical_events ALTER COLUMN \"EventTime\" TYPE text USING \"EventTime\"::text;",
            script);

        var downStart = source.IndexOf("protected override void Down", StringComparison.Ordinal);
        Assert.True(downStart > 0);
        Assert.Contains("nullable: true", source[..downStart]);
        Assert.DoesNotContain("SET NOT NULL", source[downStart..]);
    }

    private static string GenerateScript()
    {
        var options = new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseNpgsql("Host=localhost;Database=migration_script;Username=test;Password=test")
            .Options;

        using var dbContext = new SpeakEaseDbContext(options);
        return dbContext.GetService<IMigrator>().GenerateScript();
    }
}
