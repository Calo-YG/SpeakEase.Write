using System.Threading.Channels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SpeakEase.Write.Application.Abstractions.Story;
using SpeakEase.Write.Infrastructure.AI.Runtime;

namespace SpeakEase.Write.Infrastructure.AI.Character;

public sealed class CharacterRuntimeWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<CharacterRuntimeWorker> logger,
    IOptions<AgentRuntimeModeOptions> options = null) : BackgroundService, ICharacterRuntimeQueue
{
    private readonly AgentRuntimeModeOptions _options = options?.Value ?? new AgentRuntimeModeOptions
    {
        EnableCharacterSelfGrowth = true
    };
    private readonly Channel<CharacterStateRefreshRequest> _queue = Channel.CreateBounded<CharacterStateRefreshRequest>(
        new BoundedChannelOptions(256)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    public ValueTask EnqueueAsync(
        CharacterStateRefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _queue.Writer.WriteAsync(request, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            if (!_options.EnableCharacterSelfGrowth)
                continue;

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var processor = scope.ServiceProvider.GetRequiredService<ICharacterRuntimeProcessor>();
                    await processor.ProcessAsync(request, stoppingToken);
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
                        "Character state refresh attempt failed: Attempt={Attempt}, UserId={UserId}, WorkId={WorkId}, CharacterId={CharacterId}, RunId={RunId}",
                        attempt,
                        request.UserId,
                        request.WorkId,
                        request.Proposal?.CharacterId ?? string.Empty,
                        request.SourceRunId);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Character state refresh failed after retries: UserId={UserId}, WorkId={WorkId}, CharacterId={CharacterId}, RunId={RunId}",
                        request.UserId,
                        request.WorkId,
                        request.Proposal?.CharacterId ?? string.Empty,
                        request.SourceRunId);
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }
}
