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
    private readonly Channel<MemoryRefreshRequest> _channel = Channel.CreateBounded<MemoryRefreshRequest>(
        new BoundedChannelOptions(512)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(
        MemoryRefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 记忆刷新发生在消息事务提交之后，不应受客户端请求取消影响，也不应阻塞 Chat。
        if (!_channel.Writer.TryWrite(request))
        {
            logger.LogWarning(
                "Memory refresh queue is full or stopped; refresh deferred/lost: WorkId={WorkId}, SessionId={SessionId}, Turn={Turn}, RunId={RunId}",
                request.WorkId,
                request.SessionId,
                request.TurnNumber,
                request.RunId);
        }

        return ValueTask.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
        {
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
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }
}
