using Azure.Messaging.EventHubs.Processor;

namespace EventHubConsumer.Services;

public interface IPartitionQueueManager
{
    Task EnqueueAsync(QueuedEvent queuedEvent, CancellationToken cancellationToken);

    Task CompleteAndDrainAsync(CancellationToken cancellationToken);
}
