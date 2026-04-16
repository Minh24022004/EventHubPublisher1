using EventHubConsumer.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddEventHubConsumerServices(builder.Configuration);

await builder.Build().RunAsync();
