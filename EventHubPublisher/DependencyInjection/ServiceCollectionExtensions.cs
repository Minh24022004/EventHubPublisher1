using EventHubPublisher.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EventHub.Core.DependencyInjection;

namespace EventHubPublisher.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventHubPublisherServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddEventHubPublisherCore(configuration);

        services.AddSingleton<IEventPublisherService, EventPublisherService>();
        services.AddHostedService<PublisherConsoleWorker>();

        return services;
    }
}
