using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using EventHubPublisher;
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

        await using var producerClient =
            new EventHubProducerClient(
                options.ConnectionString,
                options.EventHubName);

        Console.WriteLine("Type message, /spam to send 100 messages, /exit to quit");

        int counter = 0;

        while (true)
        {
            string message = Console.ReadLine();

            if (message?.ToLower() == "/exit")
            {
                Console.WriteLine("Exit...");
                break;
            }

            if (message?.ToLower() == "/spam")
            {
                using EventDataBatch batch =
                    await producerClient.CreateBatchAsync();

                for (int i = 0; i < 100; i++)
                {
                    string spamMessage = $"Spam message {counter}";

                    EventData eventData =
                        new EventData(Encoding.UTF8.GetBytes(spamMessage));

                    if (!batch.TryAdd(eventData))
                        break;

                    counter++;
                }

                await producerClient.SendAsync(batch);

                Console.WriteLine($"Sent 100 spam messages at {DateTime.Now}");
            }
            else
            {
                using EventDataBatch batch =
                    await producerClient.CreateBatchAsync();

                EventData eventData =
                    new EventData(Encoding.UTF8.GetBytes(message));

                if (batch.TryAdd(eventData))
                {
                    await producerClient.SendAsync(batch);
                    Console.WriteLine($"Sent: {message} at {DateTime.Now}");
                }
            }
        }
    }
}

