using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using AINWZ.Tests.AI;

using SpeakEase.AI.Lib;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.Authorization;
using SpeakEase.Write.Application.Abstractions.Identity;
using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Infrastructure.AI;
using SpeakEase.Write.Infrastructure.AI.Tools;
using SpeakEase.Write.Infrastructure.Authorization;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace AINWZ.Tests.Security;

public sealed class CharacterGraphToolSecurityTests
{
    [Theory]
    [InlineData("work-2", "graph-1")]
    [InlineData("work-1", "graph-2")]
    public async Task UpdateNode_RejectsNodeOutsideRequestedWorkAndGraph(
        string nodeWorkId,
        string nodeGraphId)
    {
        await using var harness = await CharacterGraphToolHarness.CreateAsync();
        await harness.SeedAsync(db =>
        {
            AddOwnedWorkAndGraph(db);
            db.CharacterGraphNodes.Add(Node("node-foreign", nodeWorkId, nodeGraphId, "original"));
        });

        var result = await harness.ExecuteAsync(
            CreateCharacterGraphNodeTool.ToolDefinition.Function.Name,
            """{"work_id":"work-1","graph_id":"graph-1","id":"node-foreign","node_type":"mutated"}""");

        Assert.False(result.Success);
        Assert.Equal(
            "original",
            await harness.QueryAsync(db => db.CharacterGraphNodes
                .AsNoTracking()
                .Where(x => x.Id == "node-foreign")
                .Select(x => x.NodeType)
                .SingleAsync()));
    }

    [Theory]
    [InlineData("work-2", "graph-1")]
    [InlineData("work-1", "graph-2")]
    public async Task UpdateEdge_RejectsEdgeOutsideRequestedWorkAndGraph(
        string edgeWorkId,
        string edgeGraphId)
    {
        await using var harness = await CharacterGraphToolHarness.CreateAsync();
        await harness.SeedAsync(db =>
        {
            AddOwnedWorkAndGraph(db);
            db.CharacterGraphEdges.Add(Edge(
                "edge-foreign",
                edgeWorkId,
                edgeGraphId,
                "source",
                "target",
                "original"));
        });

        var result = await harness.ExecuteAsync(
            CreateCharacterGraphEdgeTool.ToolDefinition.Function.Name,
            """{"work_id":"work-1","graph_id":"graph-1","id":"edge-foreign","label":"mutated"}""");

        Assert.False(result.Success);
        Assert.Equal("edge_not_found", result.ErrorCode);
        Assert.Equal(
            "original",
            await harness.QueryAsync(db => db.CharacterGraphEdges
                .AsNoTracking()
                .Where(x => x.Id == "edge-foreign")
                .Select(x => x.Label)
                .SingleAsync()));
    }

    [Theory]
    [InlineData(true, "work-2", "graph-1")]
    [InlineData(false, "work-2", "graph-1")]
    [InlineData(true, "work-1", "graph-2")]
    [InlineData(false, "work-1", "graph-2")]
    public async Task CreateEdge_RejectsEndpointOutsideRequestedWorkAndGraph(
        bool invalidSource,
        string endpointWorkId,
        string endpointGraphId)
    {
        await using var harness = await CharacterGraphToolHarness.CreateAsync();
        await harness.SeedAsync(db =>
        {
            AddOwnedWorkAndGraph(db);
            db.CharacterGraphNodes.Add(Node("node-valid", "work-1", "graph-1", "valid"));
            db.CharacterGraphNodes.Add(Node("node-invalid", endpointWorkId, endpointGraphId, "invalid"));
        });
        var sourceId = invalidSource ? "node-invalid" : "node-valid";
        var targetId = invalidSource ? "node-valid" : "node-invalid";

        var result = await harness.ExecuteAsync(
            CreateCharacterGraphEdgeTool.ToolDefinition.Function.Name,
            $$"""{"work_id":"work-1","graph_id":"graph-1","source_node_id":"{{sourceId}}","target_node_id":"{{targetId}}","relation_type":"friend"}""");

        Assert.False(result.Success);
        Assert.Equal(0, await harness.QueryAsync(db => db.CharacterGraphEdges.AsNoTracking().CountAsync()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateEdgeByEndpoints_RejectsEndpointOutsideRequestedWorkAndGraph(bool invalidSource)
    {
        await using var harness = await CharacterGraphToolHarness.CreateAsync();
        var sourceId = invalidSource ? "node-invalid" : "node-valid";
        var targetId = invalidSource ? "node-valid" : "node-invalid";
        await harness.SeedAsync(db =>
        {
            AddOwnedWorkAndGraph(db);
            db.CharacterGraphNodes.Add(Node("node-valid", "work-1", "graph-1", "valid"));
            db.CharacterGraphNodes.Add(Node("node-invalid", "work-2", "graph-1", "invalid"));
            db.CharacterGraphEdges.Add(Edge(
                "edge-existing",
                "work-1",
                "graph-1",
                sourceId,
                targetId,
                "original"));
        });

        var result = await harness.ExecuteAsync(
            CreateCharacterGraphEdgeTool.ToolDefinition.Function.Name,
            $$"""{"work_id":"work-1","graph_id":"graph-1","source_node_id":"{{sourceId}}","target_node_id":"{{targetId}}","relation_type":"friend","label":"mutated"}""");

        Assert.False(result.Success);
        Assert.Equal(
            "original",
            await harness.QueryAsync(db => db.CharacterGraphEdges
                .AsNoTracking()
                .Where(x => x.Id == "edge-existing")
                .Select(x => x.Label)
                .SingleAsync()));
    }

    [Theory]
    [InlineData(true, "work-2", "graph-1")]
    [InlineData(false, "work-2", "graph-1")]
    [InlineData(true, "work-1", "graph-2")]
    [InlineData(false, "work-1", "graph-2")]
    public async Task CreateEdgeByCharacterNames_RejectsEndpointOutsideRequestedWorkAndGraph(
        bool invalidSource,
        string endpointWorkId,
        string endpointGraphId)
    {
        await using var harness = await CharacterGraphToolHarness.CreateAsync();
        await harness.SeedAsync(db =>
        {
            AddOwnedWorkAndGraph(db);
            var validId = invalidSource ? "target-valid" : "source-valid";
            var validName = invalidSource ? "Target" : "Source";
            var invalidId = invalidSource ? "source-invalid" : "target-invalid";
            var invalidName = invalidSource ? "Source" : "Target";
            db.CharacterGraphNodes.Add(Node(
                validId,
                "work-1",
                "graph-1",
                "valid",
                validName));
            db.CharacterGraphNodes.Add(Node(
                invalidId,
                endpointWorkId,
                endpointGraphId,
                "invalid",
                invalidName));
        });

        var result = await harness.ExecuteAsync(
            CreateCharacterGraphEdgeTool.ToolDefinition.Function.Name,
            """{"work_id":"work-1","graph_id":"graph-1","source_character_name":"Source","target_character_name":"Target","relation_type":"friend"}""");

        Assert.False(result.Success);
        Assert.Equal(
            invalidSource ? "source_not_in_graph" : "target_not_in_graph",
            result.ErrorCode);
        Assert.Equal(0, await harness.QueryAsync(db => db.CharacterGraphEdges.AsNoTracking().CountAsync()));
    }

    [Fact]
    public async Task UpdateNode_SucceedsWithinRequestedWorkAndGraph()
    {
        await using var harness = await CharacterGraphToolHarness.CreateAsync();
        await harness.SeedAsync(db =>
        {
            AddOwnedWorkAndGraph(db);
            db.CharacterGraphNodes.Add(Node("node-owned", "work-1", "graph-1", "original"));
        });

        var result = await harness.ExecuteAsync(
            CreateCharacterGraphNodeTool.ToolDefinition.Function.Name,
            """{"work_id":"work-1","graph_id":"graph-1","id":"node-owned","node_type":"protagonist"}""");

        Assert.True(result.Success, result.Content);
        Assert.Equal(
            "protagonist",
            await harness.QueryAsync(db => db.CharacterGraphNodes
                .AsNoTracking()
                .Where(x => x.Id == "node-owned")
                .Select(x => x.NodeType)
                .SingleAsync()));
    }

    [Fact]
    public async Task CreateAndUpdateEdgeByNodeIds_SucceedsWithinRequestedWorkAndGraph()
    {
        await using var harness = await CharacterGraphToolHarness.CreateAsync();
        await harness.SeedAsync(db =>
        {
            AddOwnedWorkAndGraph(db);
            db.CharacterGraphNodes.Add(Node("source-owned", "work-1", "graph-1", "valid"));
            db.CharacterGraphNodes.Add(Node("target-owned", "work-1", "graph-1", "valid"));
        });

        var createResult = await harness.ExecuteAsync(
            CreateCharacterGraphEdgeTool.ToolDefinition.Function.Name,
            """{"work_id":"work-1","graph_id":"graph-1","source_node_id":"source-owned","target_node_id":"target-owned","relation_type":"friend","label":"original"}""");
        var updateResult = await harness.ExecuteAsync(
            CreateCharacterGraphEdgeTool.ToolDefinition.Function.Name,
            """{"work_id":"work-1","graph_id":"graph-1","source_node_id":"source-owned","target_node_id":"target-owned","relation_type":"ally","label":"updated"}""");

        Assert.True(createResult.Success, createResult.Content);
        Assert.True(updateResult.Success, updateResult.Content);
        Assert.Equal(1, await harness.QueryAsync(db => db.CharacterGraphEdges.AsNoTracking().CountAsync()));
        Assert.Equal(
            "updated",
            await harness.QueryAsync(db => db.CharacterGraphEdges
                .AsNoTracking()
                .Select(x => x.Label)
                .SingleAsync()));
    }

    [Fact]
    public async Task UpdateEdgeById_SucceedsWithinRequestedWorkAndGraph()
    {
        await using var harness = await CharacterGraphToolHarness.CreateAsync();
        await harness.SeedAsync(db =>
        {
            AddOwnedWorkAndGraph(db);
            db.CharacterGraphEdges.Add(Edge(
                "edge-owned",
                "work-1",
                "graph-1",
                "source-owned",
                "target-owned",
                "original"));
        });

        var result = await harness.ExecuteAsync(
            CreateCharacterGraphEdgeTool.ToolDefinition.Function.Name,
            """{"work_id":"work-1","graph_id":"graph-1","id":"edge-owned","label":"updated"}""");

        Assert.True(result.Success, result.Content);
        Assert.Equal(
            "updated",
            await harness.QueryAsync(db => db.CharacterGraphEdges
                .AsNoTracking()
                .Where(x => x.Id == "edge-owned")
                .Select(x => x.Label)
                .SingleAsync()));
    }

    [Fact]
    public async Task CreateEdgeByCharacterNames_SucceedsWithinRequestedWorkAndGraph()
    {
        await using var harness = await CharacterGraphToolHarness.CreateAsync();
        await harness.SeedAsync(db =>
        {
            AddOwnedWorkAndGraph(db);
            db.CharacterGraphNodes.Add(Node(
                "source-owned",
                "work-1",
                "graph-1",
                "valid",
                "Source"));
            db.CharacterGraphNodes.Add(Node(
                "target-owned",
                "work-1",
                "graph-1",
                "valid",
                "Target"));
        });

        var result = await harness.ExecuteAsync(
            CreateCharacterGraphEdgeTool.ToolDefinition.Function.Name,
            """{"work_id":"work-1","graph_id":"graph-1","source_character_name":"Source","target_character_name":"Target","relation_type":"friend"}""");

        Assert.True(result.Success, result.Content);
        Assert.Equal(1, await harness.QueryAsync(db => db.CharacterGraphEdges.AsNoTracking().CountAsync()));
    }

    private static void AddOwnedWorkAndGraph(SpeakEaseDbContext db)
    {
        db.Works.Add(new WorkEntity
        {
            Id = "work-1",
            UserId = "user-1",
            Title = "Owned work"
        });
        db.CharacterGraphs.Add(new CharacterGraphEntity
        {
            Id = "graph-1",
            WorkId = "work-1",
            OwnerId = "user-1",
            Name = "Owned graph"
        });
    }

    private static CharacterGraphNodeEntity Node(
        string id,
        string workId,
        string graphId,
        string nodeType,
        string displayName = null)
    {
        return new CharacterGraphNodeEntity
        {
            Id = id,
            WorkId = workId,
            GraphId = graphId,
            OwnerId = "user-1",
            CharacterId = $"character-{id}",
            DisplayName = displayName ?? id,
            NodeType = nodeType
        };
    }

    private static CharacterGraphEdgeEntity Edge(
        string id,
        string workId,
        string graphId,
        string sourceNodeId,
        string targetNodeId,
        string label)
    {
        return new CharacterGraphEdgeEntity
        {
            Id = id,
            WorkId = workId,
            GraphId = graphId,
            OwnerId = "user-1",
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            RelationType = "friend",
            Label = label
        };
    }

    private sealed class CharacterGraphToolHarness : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ServiceProvider provider;
        private readonly ToolCapable toolCapable;

        private CharacterGraphToolHarness(SqliteConnection connection, ServiceProvider provider)
        {
            this.connection = connection;
            this.provider = provider;
            toolCapable = new ToolCapable(provider);
        }

        public static async Task<CharacterGraphToolHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddDbContext<SpeakEaseDbContext>(options => options.UseSqlite(connection));
            services.AddScoped<IWriteDbContext>(sp => sp.GetRequiredService<SpeakEaseDbContext>());
            services.AddScoped<IWorkAccessChecker, WorkAccessChecker>();
            services.AddScoped<IToolExecutionGuard, WorkToolExecutionGuard>();
            services.AddSingleton<IUserContext>(new TestUserContext("user-1"));
            services.AddSingleton<ISnowflakeIdGenerator, SequentialIdGenerator>();
            services.AddKeyedTransient<IToolExecutor, CreateCharacterGraphNodeTool>(
                CreateCharacterGraphNodeTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, CreateCharacterGraphEdgeTool>(
                CreateCharacterGraphEdgeTool.ToolDefinition.Function.Name);
            var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>()
                .Database.EnsureCreatedAsync();
            return new CharacterGraphToolHarness(connection, provider);
        }

        public async Task SeedAsync(Action<SpeakEaseDbContext> seed)
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
            seed(db);
            await db.SaveChangesAsync();
        }

        public Task<ToolResult> ExecuteAsync(string toolName, string arguments)
        {
            return toolCapable.ExecuteAsync(new ToolCall
            {
                Id = Guid.NewGuid().ToString(),
                Function = new FunctionCallDetail
                {
                    Name = toolName,
                    Arguments = arguments
                }
            }, CancellationToken.None);
        }

        public async Task<T> QueryAsync<T>(Func<SpeakEaseDbContext, Task<T>> query)
        {
            await using var scope = provider.CreateAsyncScope();
            return await query(scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>());
        }

        public async ValueTask DisposeAsync()
        {
            await provider.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
