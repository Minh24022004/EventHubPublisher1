using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using EventHubConsumer;

class Program
{
    
    private const int CHECKPOINT_BATCH_SIZE = 50;
    private static readonly ConcurrentDictionary<string, int> EventCount = new();
    private static readonly ConcurrentDictionary<string, ProcessEventArgs> LastEvent = new();

    private readonly record struct QueuedEvent(ProcessEventArgs Args, string Partition, string Message);

    private sealed class PartitionQueue
    {
        private readonly Channel<QueuedEvent> _channel;
        private readonly Func<ProcessEventArgs, string, string, Task> _processOne;

        public Task ProcessingTask { get; }

        public PartitionQueue(
            string partitionId,
            Func<ProcessEventArgs, string, string, Task> processOne)
        {
            _processOne = processOne;
            _channel = Channel.CreateUnbounded<QueuedEvent>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            ProcessingTask = ProcessLoopAsync(partitionId);
        }

        private async Task ProcessLoopAsync(string partitionId)
        {
            try
            {
                while (await _channel.Reader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (_channel.Reader.TryRead(out var item))
                    {
                        try
                        {
                            await _processOne(
                                    item.Args,
                                    item.Partition,
                                    item.Message)
                                .ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Partition {partitionId} worker: {ex.Message}");
                        }
                    }
                }
            }
            catch (ChannelClosedException)
            {
            }
        }

        public bool TryEnqueue(in QueuedEvent ev) => _channel.Writer.TryWrite(ev);

        public void CompleteWriter() => _channel.Writer.TryComplete();
    }

    static async Task Main()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var eventHubOptions = config
            .GetSection("EventHub")
            .Get<EventHubOptions>();

        var blobOptions = config
            .GetSection("BlobStorage")
            .Get<BlobStorageOptions>();

        var containerClient = new BlobContainerClient(
            blobOptions!.ConnectionString,
            blobOptions.ContainerName);

        await containerClient.CreateIfNotExistsAsync();

        var processorOptions = new EventProcessorClientOptions
        {
            LoadBalancingUpdateInterval = TimeSpan.FromSeconds(5),
            PartitionOwnershipExpirationInterval = TimeSpan.FromSeconds(15)
        };

        var processor = new EventProcessorClient(
            containerClient,
            eventHubOptions!.ConsumerGroup,
            eventHubOptions.ConnectionString,
            eventHubOptions.EventHubName,
            processorOptions);

        var partitionQueues =
            new ConcurrentDictionary<string, Lazy<PartitionQueue>>();

        
        using var runCts = new CancellationTokenSource();
        runCts.CancelAfter(TimeSpan.FromMinutes(5));

        void OnCancelKeyPress(object? _, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            runCts.Cancel();
        }

        Console.CancelKeyPress += OnCancelKeyPress;

        async Task ProcessQueuedAsync(ProcessEventArgs args, string partition, string message)
        {
            try
            {
                await Task.Delay(1000, CancellationToken.None).ConfigureAwait(false);

                EventCount.AddOrUpdate(partition, 1, (_, count) => count + 1);
                LastEvent[partition] = args;
                if (EventCount[partition] >= CHECKPOINT_BATCH_SIZE)
                {
                    var count = EventCount[partition];
                    await args.UpdateCheckpointAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                    Console.WriteLine($"[{partition}] checkpoint OK (batch {count})");
                    EventCount[partition] = 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Processing error: {ex.Message}");
                Console.WriteLine($"[{partition}] checkpoint/work error: {ex.Message}");
            }
        }

        Task ProcessEventHandler(ProcessEventArgs args)
        {
            try
            {
                if (args.CancellationToken.IsCancellationRequested)
                    return Task.CompletedTask;

                string partition = args.Partition.PartitionId;
                string message = Encoding.UTF8.GetString(args.Data.EventBody.ToArray());

                Console.WriteLine($"[{partition}] received {message} | {DateTime.Now:O}");

                var queue = partitionQueues.GetOrAdd(
                    partition,
                    p => new Lazy<PartitionQueue>(
                        () => new PartitionQueue(p, ProcessQueuedAsync),
                        LazyThreadSafetyMode.ExecutionAndPublication));

                if (!queue.Value.TryEnqueue(new QueuedEvent(args, partition, message)))
                    Debug.WriteLine($"Enqueue failed for partition {partition}.");

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Handler error: {ex.Message}");
                return Task.CompletedTask;
            }
        }

        Task ProcessErrorHandler(ProcessErrorEventArgs args)
        {
            try
            {
                Debug.WriteLine("Error in EventProcessorClient");
                Debug.WriteLine($"\tPartition: {args.PartitionId}");
                Debug.WriteLine($"\tOperation: {args.Operation}");
                Debug.WriteLine($"\tException: {args.Exception.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handler failed: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        try
        {
            processor.ProcessEventAsync += ProcessEventHandler;
            processor.ProcessErrorAsync += ProcessErrorHandler;

            try
            {
                Console.WriteLine("Starting processor...");
                await processor.StartProcessingAsync(runCts.Token);
                await Task.Delay(Timeout.Infinite, runCts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Processing stopped.");
            }
            finally
            {
                await processor.StopProcessingAsync();
                foreach (var lazy in partitionQueues.Values)
                {
                    if (lazy.IsValueCreated)
                        lazy.Value.CompleteWriter();
                }

                var drainTasks = partitionQueues.Values
                    .Where(l => l.IsValueCreated)
                    .Select(l => l.Value.ProcessingTask)
                    .ToArray();
                if (drainTasks.Length > 0)
                    await Task.WhenAll(drainTasks).ConfigureAwait(false);

                foreach (var kv in LastEvent.ToArray())
                {
                    var p = kv.Key;
                    if (EventCount.TryGetValue(p, out var n) && n > 0
                        && LastEvent.TryGetValue(p, out var last))
                    {
                        try
                        {
                            await last.UpdateCheckpointAsync(CancellationToken.None)
                                .ConfigureAwait(false);
                            Console.WriteLine($"[{p}] final checkpoint OK ({n} pending in batch).");
                            EventCount[p] = 0;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Final checkpoint failed {p}: {ex.Message}");
                            Console.WriteLine($"[{p}] final checkpoint failed: {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
            processor.ProcessEventAsync -= ProcessEventHandler;
            processor.ProcessErrorAsync -= ProcessErrorHandler;
        }
    }
}
