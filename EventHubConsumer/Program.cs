using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Threading.Channels;

class Program
{
    static DateTime lastProcessedTime = DateTime.MinValue;
    static string checkpointFile = "checkpoint.txt";

    static async Task Main()
    {
        var config = new ConfigurationBuilder()
           .AddJsonFile("appsettings.json", optional: false)
           .Build();

        string connectionString = config["EventHub:ConnectionString"];
        string eventHubName = config["EventHub:EventHubName"];

     
        if (File.Exists(checkpointFile))
        {
            string text = File.ReadAllText(checkpointFile);
            if (DateTime.TryParse(text, out DateTime savedTime))
            {
                lastProcessedTime = savedTime;
            }
        }

        await using var consumer = new EventHubConsumerClient(
           EventHubConsumerClient.DefaultConsumerGroupName,
           connectionString,
           eventHubName);

        Console.WriteLine($"Last processed time: {lastProcessedTime}");

        var channel = Channel.CreateUnbounded<EventData>();

        
        for (int i = 0; i < 50; i++)
        {
            _ = Task.Run(async () =>
            {
                await foreach (var evt in channel.Reader.ReadAllAsync())
                {
                    string message = Encoding.UTF8.GetString(evt.Body.ToArray());
                    DateTime eventTime = evt.EnqueuedTime.UtcDateTime;

                    Console.WriteLine($"Processing: {message} + {DateTime.Now}");

                    lastProcessedTime = eventTime;
                    File.WriteAllText(checkpointFile, lastProcessedTime.ToString("O"));

                    await Task.Delay(1000); 
                }
            });
        }

        EventPosition startPosition =
            lastProcessedTime == DateTime.MinValue
            ? EventPosition.Earliest
            : EventPosition.FromEnqueuedTime(new DateTimeOffset(lastProcessedTime));

        await foreach (PartitionEvent partitionEvent in
            consumer.ReadEventsFromPartitionAsync("0", startPosition))
        {
            await channel.Writer.WriteAsync(partitionEvent.Data);          
        }
    }
}
