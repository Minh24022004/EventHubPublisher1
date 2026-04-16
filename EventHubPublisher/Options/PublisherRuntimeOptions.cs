using System.ComponentModel.DataAnnotations;

namespace EventHubPublisher;

public sealed class PublisherRuntimeOptions
{
    public const string SectionName = "PublisherRuntime";

    [Range(1, int.MaxValue)]
    public int SpamMessageCount { get; set; } = 100;
}
