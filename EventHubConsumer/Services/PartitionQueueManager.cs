using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventHubConsumer.Services;

public sealed class PartitionQueueManager : IPartitionQueueManager
{
    private readonly ConcurrentDictionary<string, Lazy<PartitionQueue>> _partitionQueues = new();
    private readonly IEventProcessingService _eventProcessingService;
    private readonly ILogger<PartitionQueueManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConsumerRuntimeOptions _runtimeOptions;

    public PartitionQueueManager(
        IEventProcessingService eventProcessingService,
        ILogger<PartitionQueueManager> logger,
        ILoggerFactory loggerFactory,
        IOptions<ConsumerRuntimeOptions> runtimeOptions)
    {
        _eventProcessingService = eventProcessingService;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _runtimeOptions = runtimeOptions.Value;
    }

    public Task EnqueueAsync(QueuedEvent queuedEvent, CancellationToken cancellationToken)
    {
        var queue = _partitionQueues.GetOrAdd(
            queuedEvent.Partition,
            partitionId => new Lazy<PartitionQueue>(
                () => new PartitionQueue(
                    partitionId,
                    _eventProcessingService,
                    _loggerFactory.CreateLogger<PartitionQueue>(),
                    _runtimeOptions.PartitionQueueCapacity),
                LazyThreadSafetyMode.ExecutionAndPublication));

        return queue.Value.EnqueueAsync(queuedEvent, cancellationToken);
    }

    public async Task CompleteAndDrainAsync(CancellationToken cancellationToken)
    {
        foreach (var lazyQueue in _partitionQueues.Values)
        {
            if (lazyQueue.IsValueCreated)
            {
                lazyQueue.Value.CompleteWriter();
            }
        }

        var drainTasks = _partitionQueues.Values
            .Where(static q => q.IsValueCreated)
            .Select(static q => q.Value.ProcessingTask)
            .ToArray();

        if (drainTasks.Length == 0)
        {
            return;
        }

        await Task.WhenAll(drainTasks).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed class PartitionQueue
    {
        private readonly Channel<QueuedEvent> _channel;
        private readonly IEventProcessingService _eventProcessingService;
        private readonly ILogger _logger;
        private readonly string _partitionId;

        public PartitionQueue(
            string partitionId,
            IEventProcessingService eventProcessingService,
            ILogger logger,
            int partitionQueueCapacity)
        {
            _partitionId = partitionId;
            _eventProcessingService = eventProcessingService;
            _logger = logger;
            _channel = Channel.CreateBounded<QueuedEvent>(new BoundedChannelOptions(partitionQueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });

            ProcessingTask = ProcessLoopAsync();
        }

        public Task ProcessingTask { get; }

        public Task EnqueueAsync(QueuedEvent queuedEvent, CancellationToken cancellationToken) =>
            _channel.Writer.WriteAsync(queuedEvent, cancellationToken).AsTask();

        public void CompleteWriter() => _channel.Writer.TryComplete();

        private async Task ProcessLoopAsync()
        {
            await foreach (var item in _channel.Reader.ReadAllAsync())
            {
                try
                {
                    await _eventProcessingService.ProcessAsync(item, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Partition {PartitionId} worker failed.", _partitionId);
                }
            }
        }
    }
}
