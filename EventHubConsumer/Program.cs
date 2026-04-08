// See https://aka.ms/new-console-template for more information
using Azure.Messaging.EventHubs.Producer;
using Azure.Messaging.EventHubs;
using System.Text;
using Microsoft.Extensions.Configuration;

class program
{
  

    static async Task Main()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
        string connectionString = config["EventHub:ConnectionString"];
        string eventHubName = config["EventHub:EventHubName"];

        await using var producerClient =
            new EventHubProducerClient(connectionString, eventHubName);

        while (true)
        {
            String message = Console.ReadLine();
            if (message?.ToLower() == "exit")
            {
                Console.WriteLine("Exit...");
                break;
            }            
            using EventDataBatch batch = await producerClient.CreateBatchAsync();

            EventData eventData = new EventData(Encoding.UTF8.GetBytes(message));

            if (batch.TryAdd(eventData))
            {
                await producerClient.SendAsync(batch);
                Console.WriteLine($"Sent: {message}");
            }
        }
    }
    }