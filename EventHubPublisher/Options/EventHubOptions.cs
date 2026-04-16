using System.ComponentModel.DataAnnotations;

namespace EventHubPublisher
{
    public class EventHubOptions
    {
        public const string SectionName = "EventHub";

        [Required]
        public string ConnectionString { get; set; } = string.Empty;

        [Required]
        public string EventHubName { get; set; } = string.Empty;
    }
}
