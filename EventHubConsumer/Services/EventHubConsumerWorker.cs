using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;

namespace EventHubConsumer.Services;

public sealed class EventHubConsumerWorker : BackgroundService
{
    private readonly EventProcessorClient _processorClient;
    private readonly BlobContainerClient _blobContainerClient;
    private readonly IPartitionQueueManager _partitionQueueManager;
    private readonly IEventProcessingService _eventProcessingService;
    private readonly ILogger<EventHubConsumerWorker> _logger;

    public EventHubConsumerWorker(
        EventProcessorClient processorClient,
        BlobContainerClient blobContainerClient,
        IPartitionQueueManager partitionQueueManager,
        IEventProcessingService eventProcessingService,
        ILogger<EventHubConsumerWorker> logger)
    {
        _processorClient = processorClient;
        _blobContainerClient = blobContainerClient;
        _partitionQueueManager = partitionQueueManager;
        _eventProcessingService = eventProcessingService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processorClient.ProcessEventAsync += ProcessEventHandler;
        _processorClient.ProcessErrorAsync += ProcessErrorHandler;

        try
        {
            await _blobContainerClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken).ConfigureAwait(false);

            _logger.LogInformation("Starting Event Hub consumer...");
            await _processorClient.StartProcessingAsync(stoppingToken).ConfigureAwait(false);

            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Event Hub consumer is stopping.");
        }
        finally
        {
            await StopConsumerAsync().ConfigureAwait(false);
        }
    }

    private async Task ProcessEventHandler(ProcessEventArgs args)
    {
        if (args.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        var partition = args.Partition.PartitionId;
        var message = Encoding.UTF8.GetString(args.Data.EventBody.ToArray());

        await _partitionQueueManager
            .EnqueueAsync(new QueuedEvent(args, partition, message), args.CancellationToken)
            .ConfigureAwait(false);
    }

    private Task ProcessErrorHandler(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Error in EventProcessorClient. Partition: {PartitionId}. Operation: {Operation}",
            args.PartitionId,
            args.Operation);

        return Task.CompletedTask;
    }

    private async Task StopConsumerAsync()
    {
        try
        {
            await _processorClient.StopProcessingAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop Event Hub processor cleanly.");
        }

        try
        {
            await _partitionQueueManager.CompleteAndDrainAsync(CancellationToken.None).ConfigureAwait(false);
            await _eventProcessingService.FlushPendingCheckpointsAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to drain partition queues during shutdown.");
        }
        finally
        {
            _processorClient.ProcessEventAsync -= ProcessEventHandler;
            _processorClient.ProcessErrorAsync -= ProcessErrorHandler;
        }
    }
}
