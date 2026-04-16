namespace EventHubPublisher.Services;

public interface IEventPublisherService
{
    Task SendAsync(string message, CancellationToken cancellationToken);

    Task<int> SendSpamAsync(int messageCount, CancellationToken cancellationToken);
}
