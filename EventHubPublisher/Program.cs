using EventHubPublisher.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddEventHubPublisherServices(builder.Configuration);

await builder.Build().RunAsync();

