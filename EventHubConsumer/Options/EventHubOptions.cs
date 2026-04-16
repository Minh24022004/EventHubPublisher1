using System.ComponentModel.DataAnnotations;

namespace EventHubConsumer
{
    public class EventHubOptions
    {
        public const string SectionName = "EventHub";

        [Required]
        public string ConnectionString { get; set; } = string.Empty;

        [Required]
        public string EventHubName { get; set; } = string.Empty;

        [Required]
        public string ConsumerGroup { get; set; } = string.Empty;
    }
}
