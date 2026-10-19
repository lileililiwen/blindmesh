using Blindmesh.Infrastructure;
using Blindmesh.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddBlindmeshInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IOutboxHandler, RuntimeHeartbeatHandler>();
builder.Services.AddHostedService<OutboxClaimService>();

var host = builder.Build();

await host.RunAsync();
