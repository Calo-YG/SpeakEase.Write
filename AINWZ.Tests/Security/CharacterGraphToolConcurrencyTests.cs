using System.Collections.Concurrent;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

public sealed class CharacterGraphToolConcurrencyTests
{
    [Fact]
    public async Task CreateNode_ConcurrentRequestsCreateSingleNode()
    {
        await using var harness = await ConcurrentToolHarness<CharacterGraphNodeEntity>.CreateAsync();
        await harness.SeedAsync(db =>
        {
            AddOwnedWorkAndGraph(db);
            db.Characters.Add(new CharacterEntity
            {
                Id = "character-1",
                WorkId = "work-1",
                OwnerId = "user-1",
                Name = "Hero"
            });
        });
        harness.EnableSaveOrdering();

        var calls = await Task.WhenAll(
            harness.ExecuteAsync(
                CreateCharacterGraphNodeTool.ToolDefinition.Function.Name,
                """{"work_id":"work-1","graph_id":"graph-1","character_id":"character-1","node_type":"protagonist"}"""),
            harness.ExecuteAsync(
                CreateCharacterGraphNodeTool.ToolDefinition.Function.Name,
                """{"work_id":"work-1","graph_id":"graph-1","character_id":"character-1","node_type":"protagonist"}"""))
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.All(calls, result => Assert.True(result.Success, result.Content));
        Assert.Equal(
            1,
            await harness.QueryAsync(db => db.CharacterGraphNodes
                .AsNoTracking()
                .CountAsync(x => x.WorkId == "work-1" &&
                                 x.GraphId == "graph-1" &&
                                 x.CharacterId == "character-1")));
    }

    [Fact]
    public async Task CreateEdge_ConcurrentRequestsCreateSingleEdge()
    {
        await using var harness = await ConcurrentToolHarness<CharacterGraphEdgeEntity>.CreateAsync();
        await harness.SeedAsync(db =>
        {
            AddOwnedWorkAndGraph(db);
            db.CharacterGraphNodes.AddRange(
                Node("source-node", "source-character"),
                Node("target-node", "target-character"));
        });
        harness.EnableSaveOrdering();

        var calls = await Task.WhenAll(
            harness.ExecuteAsync(
                CreateCharacterGraphEdgeTool.ToolDefinition.Function.Name,
                """{"work_id":"work-1","graph_id":"graph-1","source_node_id":"source-node","target_node_id":"target-node","relation_type":"friend"}"""),
            harness.ExecuteAsync(
                CreateCharacterGraphEdgeTool.ToolDefinition.Function.Name,
                """{"work_id":"work-1","graph_id":"graph-1","source_node_id":"source-node","target_node_id":"target-node","relation_type":"friend"}"""))
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.All(calls, result => Assert.True(result.Success, result.Content));
        Assert.Equal(
            1,
            await harness.QueryAsync(db => db.CharacterGraphEdges
                .AsNoTracking()
                .CountAsync(x => x.WorkId == "work-1" &&
                                 x.GraphId == "graph-1" &&
                                 x.SourceNodeId == "source-node" &&
                                 x.TargetNodeId == "target-node")));
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

    private static CharacterGraphNodeEntity Node(string id, string characterId)
    {
        return new CharacterGraphNodeEntity
        {
            Id = id,
            WorkId = "work-1",
            GraphId = "graph-1",
            OwnerId = "user-1",
            CharacterId = characterId,
            DisplayName = id,
            NodeType = "supporting"
        };
    }

    private sealed class ConcurrentToolHarness<TEntity> : IAsyncDisposable
        where TEntity : class
    {
        private readonly SqliteConnection keeperConnection;
        private readonly ServiceProvider provider;
        private readonly OrderedInsertInterceptor<TEntity> saveInterceptor;
        private readonly ToolCapable toolCapable;

        private ConcurrentToolHarness(
            SqliteConnection keeperConnection,
            ServiceProvider provider,
            OrderedInsertInterceptor<TEntity> saveInterceptor)
        {
            this.keeperConnection = keeperConnection;
            this.provider = provider;
            this.saveInterceptor = saveInterceptor;
            toolCapable = new ToolCapable(provider);
        }

        public static async Task<ConcurrentToolHarness<TEntity>> CreateAsync()
        {
            var databaseName = $"character-graph-{Guid.NewGuid():N}";
            var connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";
            var keeperConnection = new SqliteConnection(connectionString);
            await keeperConnection.OpenAsync();
            var saveInterceptor = new OrderedInsertInterceptor<TEntity>();
            var services = new ServiceCollection();
            services.AddDbContext<SpeakEaseDbContext>(options => options
                .UseSqlite(connectionString)
                .AddInterceptors(saveInterceptor));
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
            return new ConcurrentToolHarness<TEntity>(keeperConnection, provider, saveInterceptor);
        }

        public void EnableSaveOrdering()
        {
            saveInterceptor.Enable();
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
            await keeperConnection.DisposeAsync();
        }
    }

    private sealed class OrderedInsertInterceptor<TEntity> : SaveChangesInterceptor
        where TEntity : class
    {
        private readonly ConcurrentDictionary<DbContext, int> saveOrder = new();
        private readonly TaskCompletionSource secondSaveArrived = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource firstSaveCompleted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int enabled;
        private int saveCount;

        public void Enable()
        {
            Volatile.Write(ref enabled, 1);
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var db = eventData.Context;
            if (Volatile.Read(ref enabled) == 0 ||
                db == null ||
                !db.ChangeTracker.Entries<TEntity>().Any(x => x.State == EntityState.Added))
            {
                return result;
            }

            var order = Interlocked.Increment(ref saveCount);
            saveOrder[db] = order;
            if (order == 1)
            {
                await secondSaveArrived.Task.WaitAsync(cancellationToken);
            }
            else if (order == 2)
            {
                secondSaveArrived.TrySetResult();
                await firstSaveCompleted.Task.WaitAsync(cancellationToken);
            }

            return result;
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            CompleteFirstSave(eventData.Context);
            return new ValueTask<int>(result);
        }

        public override Task SaveChangesFailedAsync(
            DbContextErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            CompleteFirstSave(eventData.Context);
            return Task.CompletedTask;
        }

        private void CompleteFirstSave(DbContext db)
        {
            if (db != null && saveOrder.TryGetValue(db, out var order) && order == 1)
                firstSaveCompleted.TrySetResult();
        }
    }
}
