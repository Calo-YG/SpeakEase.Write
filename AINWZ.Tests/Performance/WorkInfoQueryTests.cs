using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Infrastructure.AI.Tools;
using SpeakEase.Write.Infrastructure.Persistence;

namespace AINWZ.Tests.Performance;

public sealed class WorkInfoQueryTests
{
    [Fact]
    public async Task GetWorkInfoAsync_LoadsCountsWithOneDatabaseCommand()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var counter = new CommandCounter();
        var options = new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(counter)
            .Options;

        await using (var setup = new SpeakEaseDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Works.Add(new WorkEntity { Id = "work-1", UserId = "user-1", Title = "Test" });
            setup.Volumes.Add(new VolumeEntity { Id = "volume-1", WorkId = "work-1", OwnerId = "user-1", Title = "Volume 1" });
            setup.Chapters.AddRange(
                new ChapterEntity { Id = "chapter-1", WorkId = "work-1", OwnerId = "user-1", Title = "Chapter 1" },
                new ChapterEntity { Id = "chapter-2", WorkId = "work-1", OwnerId = "user-1", Title = "Chapter 2" });
            await setup.SaveChangesAsync();
        }

        counter.Reset();
        using var provider = new ServiceCollection()
            .AddScoped(_ => new SpeakEaseDbContext(options))
            .BuildServiceProvider();
        var tool = new GetWorkInfoTool(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new StaticOptionsSnapshot<JsonSerializerOptions>(ToolArgsHelper.Options));

        var result = await tool.ExecuteAsync("{\"work_id\":\"work-1\"}", CancellationToken.None);

        Assert.True(result.Success, result.Content);
        Assert.Contains("\"chapterCount\":2", result.Content);
        Assert.Contains("\"volumeCount\":1", result.Content);
        Assert.Contains("\"characterCount\":0", result.Content);
        Assert.Equal(1, counter.CommandCount);
    }

    private sealed class StaticOptionsSnapshot<T>(T value) : IOptionsSnapshot<T>
        where T : class
    {
        public T Value => value;

        public T Get(string name) => value;
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int _commandCount;

        public int CommandCount => Volatile.Read(ref _commandCount);

        public void Reset() => Interlocked.Exchange(ref _commandCount, 0);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref _commandCount);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _commandCount);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            Interlocked.Increment(ref _commandCount);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _commandCount);
            return ValueTask.FromResult(result);
        }
    }
}
