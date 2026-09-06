using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ApplicationMemoryProvider = SpeakEase.Write.Application.Abstractions.AI.IMemoryProvider;
using SpeakEase.Write.Application.Abstractions.AI;

namespace SpeakEase.Write.Infrastructure.AI.Memory;

/// <summary>
/// 会话消息提交后的后台记忆刷新队列。队列只传递不可变标识，Provider 在独立 Scope 中解析。
/// </summary>
public sealed class MemoryRefreshQueue(
    IServiceScopeFactory scopeFactory,
    ILogger<MemoryRefreshQueue> logger) : BackgroundService, IMemoryRefreshQueue
{
    private readonly object _queueGate = new();
    private readonly ConcurrentDictionary<MemoryRefreshKey, MemoryRefreshRequest> _pending = new();
    private readonly Channel<MemoryRefreshKey> _signals = Channel.CreateUnbounded<MemoryRefreshKey>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(
        MemoryRefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 记忆刷新发生在消息事务提交之后，不应受客户端请求取消影响，也不应阻塞 Chat。
        // 同一会话只保留最新请求，避免高并发下重复刷新；回滚请求也会覆盖较高轮次的请求。
        var key = new MemoryRefreshKey(request.UserId, request.WorkId, request.SessionId);
        lock (_queueGate)
        {
            var shouldSignal = !_pending.ContainsKey(key);
            _pending[key] = request;

            if (shouldSignal && !_signals.Writer.TryWrite(key))
            {
                _pending.TryRemove(key, out _);
                logger.LogWarning(
                    "Memory refresh signal queue is stopped; refresh deferred/lost: WorkId={WorkId}, SessionId={SessionId}, Turn={Turn}, RunId={RunId}",
                    request.WorkId,
                    request.SessionId,
                    request.TurnNumber,
                    request.RunId);
            }
        }

        return ValueTask.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var key in _signals.Reader.ReadAllAsync(stoppingToken))
        {
            if (!_pending.TryRemove(key, out var request))
                continue;

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var provider = scope.ServiceProvider.GetRequiredService<ApplicationMemoryProvider>();
                    await provider.RefreshAfterTurnAsync(
                        request.UserId,
                        request.WorkId,
                        request.SessionId,
                        request.TurnNumber,
                        stoppingToken);
                    break;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex) when (attempt < 3)
                {
                    logger.LogWarning(
                        ex,
                        "Memory refresh attempt failed: Attempt={Attempt}, WorkId={WorkId}, SessionId={SessionId}, Turn={Turn}, RunId={RunId}",
                        attempt,
                        request.WorkId,
                        request.SessionId,
                        request.TurnNumber,
                        request.RunId);
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Memory refresh failed after retries: WorkId={WorkId}, SessionId={SessionId}, Turn={Turn}, RunId={RunId}",
                        request.WorkId,
                        request.SessionId,
                        request.TurnNumber,
                        request.RunId);
                    await MarkStaleAsync(request);
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_queueGate)
        {
            _signals.Writer.TryComplete();
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task MarkStaleAsync(MemoryRefreshRequest request)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetService<IMemoryRefreshFailureHandler>();
            if (handler is not null)
                await handler.MarkStaleAsync(request, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to mark memory refresh as stale: WorkId={WorkId}, SessionId={SessionId}, Turn={Turn}",
                request.WorkId,
                request.SessionId,
                request.TurnNumber);
        }
    }

    private readonly record struct MemoryRefreshKey(
        string UserId,
        string WorkId,
        string SessionId);
}
