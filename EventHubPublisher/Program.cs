// See https://aka.ms/new-console-template for more information

using Azure.Messaging.EventHubs.Producer;
using Azure.Messaging.EventHubs;
using System.Text;
using Azure.Messaging.EventHubs.Consumer;
using Microsoft.Extensions.Configuration;

class Program
{

    static async Task Main()
    {
        var config = new ConfigurationBuilder()
           .AddJsonFile("appsettings.json", optional: false)
           .Build();
        string connectionString = config["EventHub:ConnectionString"];
        string eventHubName = config["EventHub:EventHubName"];
        string consumerGroup = EventHubConsumerClient.DefaultConsumerGroupName;

        await using var consumer = new EventHubConsumerClient(consumerGroup,connectionString);

        Console.WriteLine("Listening for events...");

        await foreach (PartitionEvent partitionEvent in consumer.ReadEventsAsync())
        {
            string message = Encoding.UTF8.GetString(partitionEvent.Data.Body.ToArray());

            Console.WriteLine($"Received: {message}: {DateTime.Now} ");
        }
    }
}