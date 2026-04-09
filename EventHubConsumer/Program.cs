using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using EventHubConsumer;
using Microsoft.Extensions.Configuration;
using System.Text;

class Program
{
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
        string message =
            Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray());

        Console.WriteLine(
            $"Received: {message} | {DateTime.Now} | Partition: {eventArgs.Partition.PartitionId}");

        await eventArgs.UpdateCheckpointAsync();
    }

    static Task ProcessErrorHandler(ProcessErrorEventArgs args)
    {
        Console.WriteLine(
            $"Error on partition {args.PartitionId}: {args.Exception.Message}");

        return Task.CompletedTask;
    }
}