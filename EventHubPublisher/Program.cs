// See https://aka.ms/new-console-template for more information

using Azure.Messaging.EventHubs.Producer;
using Azure.Messaging.EventHubs;
using System.Text;
using Azure.Messaging.EventHubs.Consumer;

class Program
{
    private const string connectionString = "Endpoint=sb://test-eventhub1234.servicebus.windows.net/;SharedAccessKeyName=minhtest;SharedAccessKey=jNyS9RBIwbhBvZ3O8JOIFyvNeAY5Fb51F+AEhJGkphE=;EntityPath=testeventhub1234";

    static async Task Main()
    {
        string consumerGroup = EventHubConsumerClient.DefaultConsumerGroupName;

        await using var consumer = new EventHubConsumerClient(consumerGroup,connectionString);

        Console.WriteLine("Listening for events...");

        await foreach (PartitionEvent partitionEvent in consumer.ReadEventsAsync())
        {
            string message = Encoding.UTF8.GetString(partitionEvent.Data.Body.ToArray());

            Console.WriteLine($"Received: {message}");
        }
    }
}