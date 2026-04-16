using System.ComponentModel.DataAnnotations;

namespace EventHubConsumer
{
    public class BlobStorageOptions
    {
        public const string SectionName = "BlobStorage";

        [Required]
        public string ConnectionString { get; set; } = string.Empty;

        [Required]
        public string ContainerName { get; set; } = string.Empty;
    }
}
