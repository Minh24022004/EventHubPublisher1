using Azure.Messaging.EventHubs.Processor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace EventHubConsumer.Services;

public sealed class EventProcessingService : IEventProcessingService
{
    private readonly ConcurrentDictionary<string, int> _eventCount = new();
    private readonly ConcurrentDictionary<string, ProcessEventArgs> _lastEvent = new();
    private readonly ILogger<EventProcessingService> _logger;
    private readonly ConsumerRuntimeOptions _runtimeOptions;

    public EventProcessingService(
        ILogger<EventProcessingService> logger,
        IOptions<ConsumerRuntimeOptions> runtimeOptions)
    {
        _logger = logger;
        _runtimeOptions = runtimeOptions.Value;
    }

    public async Task ProcessAsync(QueuedEvent queuedEvent, CancellationToken cancellationToken)
    {
        var partition = queuedEvent.Partition;

        try
        {
            _logger.LogInformation(
                "[{Partition}] received {Message} | {Timestamp}",
                partition,
                queuedEvent.Message,
                DateTimeOffset.Now);

            var updatedCount = _eventCount.AddOrUpdate(partition, 1, static (_, count) => count + 1);
            _lastEvent[partition] = queuedEvent.Args;

            if (updatedCount < _runtimeOptions.CheckpointBatchSize)
            {
                return;
            }

            await queuedEvent.Args.UpdateCheckpointAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("[{Partition}] checkpoint OK (batch {Count})", partition, updatedCount);
            _eventCount[partition] = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Partition}] checkpoint/work error", partition);
        }
    }

    public async Task FlushPendingCheckpointsAsync(CancellationToken cancellationToken)
    {
        foreach (var partition in _lastEvent.Keys.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_eventCount.TryGetValue(partition, out var pendingCount) || pendingCount <= 0)
            {
                continue;
            }

            if (!_lastEvent.TryGetValue(partition, out var lastEvent))
            {
                continue;
            }

            try
            {
                await lastEvent.UpdateCheckpointAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "[{Partition}] final checkpoint OK ({Count} pending in batch).",
                    partition,
                    pendingCount);
                _eventCount[partition] = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Partition}] final checkpoint failed", partition);
            }
        }
    }
}
