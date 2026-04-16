using Azure.Messaging.EventHubs.Processor;

namespace EventHubConsumer.Services;

public interface IEventProcessingService
{
    Task ProcessAsync(QueuedEvent queuedEvent, CancellationToken cancellationToken);

    Task FlushPendingCheckpointsAsync(CancellationToken cancellationToken);
}
