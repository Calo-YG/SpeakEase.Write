using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeakEase.Write.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_creation_sessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TurnCount = table.Column<int>(type: "integer", nullable: false),
                    ContextSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    AdoptedContentJson = table.Column<string>(type: "text", nullable: true),
                    MessagesJson = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_creation_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_generation_results",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TaskId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ModelId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Keywords = table.Column<string>(type: "text", nullable: true),
                    FeedbackStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_generation_results", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_generation_tasks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChapterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TaskType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: true),
                    ContextSnapshotId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PrimaryModelId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FallbackModelId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ParameterJson = table.Column<string>(type: "text", nullable: true),
                    ResultJson = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_generation_tasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_model_definitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ApiBaseUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ApiKey = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_model_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "chapter_analysis_results",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TaskId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChapterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AnalysisType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    CreatedEntityIds = table.Column<string>(type: "jsonb", nullable: true),
                    IsConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    UserFeedback = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chapter_analysis_results", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "chapter_versions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChapterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ModelId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chapter_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "chapters",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VolumeId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    WordCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    OutlineNodeIds = table.Column<string>(type: "text", nullable: true),
                    LastContentSavedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuthorNotes = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chapters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "character_arcs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CharacterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StageOrder = table.Column<int>(type: "integer", nullable: false),
                    StageTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InitialState = table.Column<string>(type: "text", nullable: true),
                    ChangedState = table.Column<string>(type: "text", nullable: true),
                    TriggerEvent = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_arcs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "character_graph_edges",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GraphId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceNodeId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetNodeId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RelationshipId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RelationType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Weight = table.Column<int>(type: "integer", nullable: false),
                    Direction = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    StyleJson = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_graph_edges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "character_graph_nodes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GraphId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CharacterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NodeType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Importance = table.Column<int>(type: "integer", nullable: false),
                    X = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Y = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    StyleJson = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_graph_nodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "character_graphs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LayoutJson = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_graphs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "character_relationships",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceCharacterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetCharacterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RelationshipType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Intensity = table.Column<int>(type: "integer", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_relationships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "characters",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Alias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Gender = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AgeDescription = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Identity = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Appearance = table.Column<string>(type: "text", nullable: true),
                    Personality = table.Column<string>(type: "text", nullable: true),
                    BackgroundStory = table.Column<string>(type: "text", nullable: true),
                    Motivation = table.Column<string>(type: "text", nullable: true),
                    AbilityDescription = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "context_assembly_logs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChapterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TaskId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ContextMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SnapshotId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PrimaryModelId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FallbackModelId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    InputTokenCount = table.Column<int>(type: "integer", nullable: false),
                    CoreSettingTokens = table.Column<int>(type: "integer", nullable: false),
                    RecentContextTokens = table.Column<int>(type: "integer", nullable: false),
                    RetrievedContextTokens = table.Column<int>(type: "integer", nullable: false),
                    SelectedChunkIdsJson = table.Column<string>(type: "text", nullable: true),
                    AssemblySummary = table.Column<string>(type: "text", nullable: true),
                    UsedFallback = table.Column<bool>(type: "boolean", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_context_assembly_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "factions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorldSettingId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FactionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    RelationshipJson = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "foreshadowings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SetupChapterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PayoffChapterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Importance = table.Column<int>(type: "integer", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_foreshadowings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "geographies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorldSettingId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GeographyType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ParentGeographyId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geographies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "historical_events",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorldSettingId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    EraLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EventTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ImpactSummary = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historical_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inspiration_records",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    InspirationType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inspiration_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "llm_call_logs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CallType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SkillName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RequestSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ResponseSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PrimaryModel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FinalModel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UsedFallback = table.Column<bool>(type: "boolean", nullable: false),
                    FallbackModel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RequestId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FinishReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ToolCallsSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ToolResultsSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    StopReason = table.Column<string>(type: "text", nullable: true),
                    Iterations = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "text", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "text", nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_call_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "memory_snapshots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChapterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SnapshotType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    SnapshotJson = table.Column<string>(type: "text", nullable: true),
                    VersionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outline_nodes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OutlineId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ParentNodeId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Goal = table.Column<string>(type: "text", nullable: true),
                    KeyEvent = table.Column<string>(type: "text", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    StageType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CharacterIds = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outline_nodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outlines",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StructureTemplate = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    JsonContent = table.Column<string>(type: "text", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outlines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "power_systems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorldSettingId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LevelDefinitionJson = table.Column<string>(type: "text", nullable: true),
                    AbilityRule = table.Column<string>(type: "text", nullable: true),
                    ResourceSystem = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_power_systems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prompt_templates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Scenario = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsSystemTemplate = table.Column<bool>(type: "boolean", nullable: false),
                    Variables = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reference_passages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReferenceWorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PassageType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Content = table.Column<string>(type: "text", nullable: true),
                    HighlightTagsJson = table.Column<string>(type: "text", nullable: true),
                    TechniqueAnalysis = table.Column<string>(type: "text", nullable: true),
                    FavoriteCount = table.Column<int>(type: "integer", nullable: false),
                    RecommendationCount = table.Column<int>(type: "integer", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reference_passages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reference_works",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Author = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Genre = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StyleTags = table.Column<string>(type: "text", nullable: true),
                    Score = table.Column<decimal>(type: "numeric", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reference_works", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "timeline_events",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChapterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    EventTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RelatedCharacterIds = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_timeline_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_ai_model_configs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConfigName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ModelName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FallbackProviderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FallbackModelName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Preference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UseFallback = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    EstimateCost = table.Column<decimal>(type: "numeric", nullable: false),
                    ContextWindow = table.Column<int>(type: "integer", nullable: false),
                    MaxOutputTokens = table.Column<int>(type: "integer", nullable: false),
                    SupportsStreaming = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsToolCall = table.Column<bool>(type: "boolean", nullable: false),
                    CapabilityTags = table.Column<string>(type: "jsonb", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_ai_model_configs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_passage_favorites",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PassageId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_passage_favorites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_preferences",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DefaultGenre = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NarrativeStyle = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WritingStyle = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EditorPreferenceJson = table.Column<string>(type: "text", nullable: true),
                    PromptPreferences = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_preferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Account = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NickName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Salt = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Password = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Avatar = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SubscriptionPlan = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "volumes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_volumes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "works",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    Genre = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StyleTags = table.Column<string>(type: "text", nullable: true),
                    Perspective = table.Column<string>(type: "text", nullable: true),
                    CreationMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CoverUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TotalWordCount = table.Column<int>(type: "integer", nullable: false),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_works", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "world_rules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorldSettingId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RuleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RuleType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ConstraintJson = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_world_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "world_settings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorldName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EraBackground = table.Column<string>(type: "text", nullable: true),
                    OverallStyle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    JsonContent = table.Column<string>(type: "text", nullable: true),
                    CreateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_world_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_creation_sessions_LastActivityAt",
                table: "ai_creation_sessions",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_ai_creation_sessions_WorkId_Status",
                table: "ai_creation_sessions",
                columns: new[] { "WorkId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_model_definitions_Provider",
                table: "ai_model_definitions",
                column: "Provider",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chapter_analysis_results_ChapterId",
                table: "chapter_analysis_results",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_chapter_analysis_results_TaskId",
                table: "chapter_analysis_results",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_chapter_analysis_results_WorkId",
                table: "chapter_analysis_results",
                column: "WorkId");

            migrationBuilder.CreateIndex(
                name: "IX_tags_Name",
                table: "tags",
                column: "Name",
                unique: true);

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
                name: "IX_user_passage_favorites_UserId_PassageId",
                table: "user_passage_favorites",
                columns: new[] { "UserId", "PassageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_creation_sessions");

            migrationBuilder.DropTable(
                name: "ai_generation_results");

            migrationBuilder.DropTable(
                name: "ai_generation_tasks");

            migrationBuilder.DropTable(
                name: "ai_model_definitions");

            migrationBuilder.DropTable(
                name: "chapter_analysis_results");

            migrationBuilder.DropTable(
                name: "chapter_versions");

            migrationBuilder.DropTable(
                name: "chapters");

            migrationBuilder.DropTable(
                name: "character_arcs");

            migrationBuilder.DropTable(
                name: "character_graph_edges");

            migrationBuilder.DropTable(
                name: "character_graph_nodes");

            migrationBuilder.DropTable(
                name: "character_graphs");

            migrationBuilder.DropTable(
                name: "character_relationships");

            migrationBuilder.DropTable(
                name: "characters");

            migrationBuilder.DropTable(
                name: "context_assembly_logs");

            migrationBuilder.DropTable(
                name: "factions");

            migrationBuilder.DropTable(
                name: "foreshadowings");

            migrationBuilder.DropTable(
                name: "geographies");

            migrationBuilder.DropTable(
                name: "historical_events");

            migrationBuilder.DropTable(
                name: "inspiration_records");

            migrationBuilder.DropTable(
                name: "llm_call_logs");

            migrationBuilder.DropTable(
                name: "memory_snapshots");

            migrationBuilder.DropTable(
                name: "outline_nodes");

            migrationBuilder.DropTable(
                name: "outlines");

            migrationBuilder.DropTable(
                name: "power_systems");

            migrationBuilder.DropTable(
                name: "prompt_templates");

            migrationBuilder.DropTable(
                name: "reference_passages");

            migrationBuilder.DropTable(
                name: "reference_works");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "timeline_events");

            migrationBuilder.DropTable(
                name: "user_ai_model_configs");

            migrationBuilder.DropTable(
                name: "user_passage_favorites");

            migrationBuilder.DropTable(
                name: "user_preferences");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "volumes");

            migrationBuilder.DropTable(
                name: "works");

            migrationBuilder.DropTable(
                name: "world_rules");

            migrationBuilder.DropTable(
                name: "world_settings");
        }
    }
}
