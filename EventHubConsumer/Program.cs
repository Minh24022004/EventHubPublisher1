// See https://aka.ms/new-console-template for more information
using Azure.Messaging.EventHubs.Producer;
using Azure.Messaging.EventHubs;
using System.Text;

class program
{
    private const string connectionString = "Endpoint=sb://test-eventhub1234.servicebus.windows.net/;SharedAccessKeyName=minhtest;SharedAccessKey=jNyS9RBIwbhBvZ3O8JOIFyvNeAY5Fb51F+AEhJGkphE=;EntityPath=testeventhub1234";
    private const string eventHubName = "testeventhub1234";

    static async Task Main()
    {
        await using var producerClient =
            new EventHubProducerClient(connectionString, eventHubName);

        int counter = 0;

        while (true)
        {
            using EventDataBatch batch = await producerClient.CreateBatchAsync();

            for (int i = 0; i < 100; i++)
            {
                string message = $"Message {counter} - {DateTime.Now}";
                EventData eventData = new EventData(Encoding.UTF8.GetBytes(message));

                if (!batch.TryAdd(eventData))
                {
                    break;
                }

                counter++;
            }

            await producerClient.SendAsync(batch);

            Console.WriteLine($"Sent batch with {batch.Count} events");

            await Task.Delay(1000); 
        }
    }
    }