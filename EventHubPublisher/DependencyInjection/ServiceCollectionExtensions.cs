using Azure.Messaging.EventHubs.Producer;
using EventHubPublisher.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventHubPublisher.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventHubPublisherServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<EventHubOptions>()
            .Bind(configuration.GetSection(EventHubOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<PublisherRuntimeOptions>()
            .Bind(configuration.GetSection(PublisherRuntimeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EventHubOptions>>().Value;
            return new EventHubProducerClient(options.ConnectionString, options.EventHubName);
        });

        services.AddSingleton<IEventPublisherService, EventPublisherService>();
        services.AddHostedService<PublisherConsoleWorker>();

        return services;
    }
}
