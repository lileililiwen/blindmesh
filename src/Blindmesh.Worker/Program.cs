using Blindmesh.Infrastructure;
using Blindmesh.Infrastructure.Capabilities;
using Blindmesh.Infrastructure.Identity;
using Blindmesh.Infrastructure.Credits;
using Blindmesh.Infrastructure.Findings;
using Blindmesh.Infrastructure.Moderation;
using Blindmesh.Infrastructure.Projects;
using Blindmesh.Infrastructure.Reputation;
using Blindmesh.Infrastructure.Tasks;
using Blindmesh.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddBlindmeshInfrastructure(builder.Configuration);

// The worker calls the same application methods as the API, so it registers
// the modules that own the tasks, the projects, the capabilities and the
// escrow of the ledger.
builder.Services.AddBlindmeshIdentity(builder.Configuration);
builder.Services.AddBlindmeshProjects();
builder.Services.AddBlindmeshCapabilities();
builder.Services.AddBlindmeshCredits();
builder.Services.AddBlindmeshTasks();
builder.Services.AddBlindmeshFindings();
// The findings module settles a seat through the moderation module, so the
// worker registers it. Without it the review deadline cycle cannot resolve
// the service and fails every cycle.
builder.Services.AddBlindmeshModeration();
builder.Services.AddBlindmeshReputation();

builder.Services.AddSingleton<IOutboxHandler, RuntimeHeartbeatHandler>();

// The handlers of the reputation module are scoped, so they share the scoped
// database context and the transaction of the cycle that claimed the message.
builder.Services.AddScoped<IOutboxHandler, ParticipationAcceptedReputationHandler>();
builder.Services.AddScoped<IOutboxHandler, FindingConfirmedReputationHandler>();
builder.Services.AddScoped<IOutboxHandler, ArbitrationReversedReputationHandler>();

builder.Services.AddHostedService<OutboxClaimService>();
builder.Services.AddHostedService<ParticipationDeadlineService>();
builder.Services.AddHostedService<ReviewDeadlineService>();

var host = builder.Build();

await host.RunAsync();
