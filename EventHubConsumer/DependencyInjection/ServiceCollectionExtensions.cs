using Azure.Messaging.EventHubs;
using Azure.Storage.Blobs;
using EventHubConsumer.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventHubConsumer.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventHubConsumerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<EventHubOptions>()
            .Bind(configuration.GetSection(EventHubOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<BlobStorageOptions>()
            .Bind(configuration.GetSection(BlobStorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<ConsumerRuntimeOptions>()
            .Bind(configuration.GetSection(ConsumerRuntimeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BlobStorageOptions>>().Value;
            return new BlobContainerClient(options.ConnectionString, options.ContainerName);
        });

        services.AddSingleton(sp =>
        {
            var eventHubOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EventHubOptions>>().Value;
            var runtimeOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ConsumerRuntimeOptions>>().Value;
            var containerClient = sp.GetRequiredService<BlobContainerClient>();

            var processorOptions = new EventProcessorClientOptions
            {
                LoadBalancingUpdateInterval = TimeSpan.FromSeconds(runtimeOptions.LoadBalancingUpdateSeconds),
                PartitionOwnershipExpirationInterval = TimeSpan.FromSeconds(runtimeOptions.PartitionOwnershipExpirationSeconds)
            };

            return new EventProcessorClient(
                containerClient,
                eventHubOptions.ConsumerGroup,
                eventHubOptions.ConnectionString,
                eventHubOptions.EventHubName,
                processorOptions);
        });

        services.AddSingleton<IEventProcessingService, EventProcessingService>();
        services.AddSingleton<IPartitionQueueManager, PartitionQueueManager>();
        services.AddHostedService<EventHubConsumerWorker>();

        return services;
    }
}
