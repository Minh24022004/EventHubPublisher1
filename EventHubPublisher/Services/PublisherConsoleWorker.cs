using Azure.Messaging.EventHubs.Producer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using EventHub.Core.Options;

namespace EventHubPublisher.Services;

public sealed class PublisherConsoleWorker : BackgroundService
{
    private readonly IEventPublisherService _eventPublisherService;
    private readonly EventHubProducerClient _producerClient;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger<PublisherConsoleWorker> _logger;
    private readonly PublisherRuntimeOptions _runtimeOptions;

    public PublisherConsoleWorker(
        IEventPublisherService eventPublisherService,
        EventHubProducerClient producerClient,
        IHostApplicationLifetime applicationLifetime,
        ILogger<PublisherConsoleWorker> logger,
        IOptions<PublisherRuntimeOptions> runtimeOptions)
    {
        _eventPublisherService = eventPublisherService;
        _producerClient = producerClient;
        _applicationLifetime = applicationLifetime;
        _logger = logger;
        _runtimeOptions = runtimeOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Type message, /spam to send 100 messages, /exit to quit");

        while (!stoppingToken.IsCancellationRequested)
        {
            string? input;

            try
            {
                input = await Console.In.ReadLineAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (input is null)
            {
                _applicationLifetime.StopApplication();
                break;
            }

            var message = input.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                continue;
            }

            if (string.Equals(message, "/exit", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Exit...");
                _applicationLifetime.StopApplication();
                break;
            }

            try
            {
                if (string.Equals(message, "/spam", StringComparison.OrdinalIgnoreCase))
                {
                    var sentCount = await _eventPublisherService
                        .SendSpamAsync(_runtimeOptions.SpamMessageCount, stoppingToken)
                        .ConfigureAwait(false);

                    _logger.LogInformation("Requested {Requested}, sent {Actual} spam messages.", _runtimeOptions.SpamMessageCount, sentCount);
                    continue;
                }

                await _eventPublisherService.SendAsync(message, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message.");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _producerClient.DisposeAsync().ConfigureAwait(false);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
