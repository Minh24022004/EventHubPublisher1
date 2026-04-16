using EventHubConsumer.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EventHub.Core.DependencyInjection;

namespace EventHubConsumer.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventHubConsumerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddEventHubConsumerCore(configuration);

        services.AddSingleton<IEventProcessingService, EventProcessingService>();
        services.AddSingleton<IPartitionQueueManager, PartitionQueueManager>();
        services.AddHostedService<EventHubConsumerWorker>();

        return services;
    }
}
