using System.ComponentModel.DataAnnotations;

namespace EventHubConsumer;

public sealed class ConsumerRuntimeOptions
{
    public const string SectionName = "ConsumerRuntime";

    [Range(1, int.MaxValue)]
    public int CheckpointBatchSize { get; set; } = 50;

    [Range(1, int.MaxValue)]
    public int PartitionQueueCapacity { get; set; } = 1000;

    [Range(1, 3600)]
    public int LoadBalancingUpdateSeconds { get; set; } = 5;

    [Range(1, 3600)]
    public int PartitionOwnershipExpirationSeconds { get; set; } = 15;
}
