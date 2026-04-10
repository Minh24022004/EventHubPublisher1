using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using EventHubConsumer;
using Microsoft.Extensions.Configuration;
using System.Text;

class Program
{
    private static readonly SemaphoreSlim _lock = new(1, 1);

    static async Task Main()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var options = config
            .GetSection("EventHub")
            .Get<EventHubOptions>();

        var blobOptions = config
            .GetSection("BlobStorage")
            .Get<BlobStorageOptions>();

        BlobContainerClient containerClient =
            new BlobContainerClient(
                blobOptions.ConnectionString,
                blobOptions.ContainerName);

        await containerClient.CreateIfNotExistsAsync();

        var processor = new EventProcessorClient(
            containerClient,
            options.ConsumerGroup,
            options.ConnectionString,
            options.EventHubName
        );

        processor.ProcessEventAsync += ProcessEventHandler;
        processor.ProcessErrorAsync += ProcessErrorHandler;

        Console.WriteLine("Starting Event Processor...");

        await processor.StartProcessingAsync();

        Console.WriteLine("Press ENTER to stop...");
        Console.ReadLine();

        await processor.StopProcessingAsync();
    }

    static async Task ProcessEventHandler(ProcessEventArgs eventArgs)
    {
        await _lock.WaitAsync();

        try
        {
            string message =
                Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray());

            await Task.Delay(100);

            Console.WriteLine(
                $"Received: {message} | {DateTime.Now} | Partition: {eventArgs.Partition.PartitionId}");

            await eventArgs.UpdateCheckpointAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    static Task ProcessErrorHandler(ProcessErrorEventArgs args)
    {
        Console.WriteLine(
            $"Error on partition {args.PartitionId}: {args.Exception.Message}");

        return Task.CompletedTask;
    }
}