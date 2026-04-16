using Azure.Messaging.EventHubs.Processor;

namespace EventHubConsumer.Services;

public readonly record struct QueuedEvent(ProcessEventArgs Args, string Partition, string Message);
