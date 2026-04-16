using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.Extensions.Logging;
using System.Text;

namespace EventHubPublisher.Services;

public sealed class EventPublisherService : IEventPublisherService
{
    private readonly EventHubProducerClient _producerClient;
    private readonly ILogger<EventPublisherService> _logger;
    private int _counter;

    public EventPublisherService(
        EventHubProducerClient producerClient,
        ILogger<EventPublisherService> logger)
    {
        _producerClient = producerClient;
        _logger = logger;
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken)
    {
        using EventDataBatch batch = await _producerClient.CreateBatchAsync(cancellationToken).ConfigureAwait(false);

        var eventData = new EventData(Encoding.UTF8.GetBytes(message));
        if (!batch.TryAdd(eventData))
        {
            throw new InvalidOperationException("Message is too large to fit in a batch.");
        }

        await _producerClient.SendAsync(batch, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Sent: {Message} at {Timestamp}", message, DateTimeOffset.Now);
    }

    public async Task<int> SendSpamAsync(int messageCount, CancellationToken cancellationToken)
    {
        using EventDataBatch batch = await _producerClient.CreateBatchAsync(cancellationToken).ConfigureAwait(false);

        var sentCount = 0;
        for (var i = 0; i < messageCount; i++)
        {
            var spamMessage = $"Spam message {_counter}";
            var eventData = new EventData(Encoding.UTF8.GetBytes(spamMessage));

            if (!batch.TryAdd(eventData))
            {
                break;
            }

            _counter++;
            sentCount++;
        }

        if (sentCount == 0)
        {
            return 0;
        }

        await _producerClient.SendAsync(batch, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Sent {Count} spam messages at {Timestamp}", sentCount, DateTimeOffset.Now);
        return sentCount;
    }
}
