using Blindmesh.Infrastructure;
using Blindmesh.Infrastructure.Capabilities;
using Blindmesh.Infrastructure.Identity;
using Blindmesh.Infrastructure.Credits;
using Blindmesh.Infrastructure.Findings;
using Blindmesh.Infrastructure.Matching;
using Blindmesh.Infrastructure.Moderation;
using Blindmesh.Infrastructure.Notifications;
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
// The notification module owns its own queue, so the worker registers it and
// sends the deliveries that are due.
builder.Services.AddBlindmeshNotifications();
// The project module of the worker asks the rule of the scope of the matching
// module, because the project service resolves the tasks of a project. Without
// it the claim cycle of the outbox cannot resolve the service and fails every
// cycle, and the projection of the reputation never runs.
builder.Services.AddBlindmeshMatching();

builder.Services.AddSingleton<IOutboxHandler, RuntimeHeartbeatHandler>();

// The handlers of the reputation module are scoped, so they share the scoped
// database context and the transaction of the cycle that claimed the message.
builder.Services.AddScoped<IOutboxHandler, ParticipationAcceptedReputationHandler>();
builder.Services.AddScoped<IOutboxHandler, FindingConfirmedReputationHandler>();
builder.Services.AddScoped<IOutboxHandler, ArbitrationReversedReputationHandler>();

// The handlers of the notification module turn the events of the workflow into
// the notifications of their parties. They are scoped, so they share the
// scoped database context and the transaction of the cycle that claimed the
// message.
builder.Services.AddScoped<IOutboxHandler, ApplicationReceivedNotificationHandler>();
builder.Services.AddScoped<IOutboxHandler, ApplicationAcceptedNotificationHandler>();
builder.Services.AddScoped<IOutboxHandler, SubmissionReceivedNotificationHandler>();
builder.Services.AddScoped<IOutboxHandler, CorrectionRequestedNotificationHandler>();
builder.Services.AddScoped<IOutboxHandler, ParticipationAcceptedNotificationHandler>();
builder.Services.AddScoped<IOutboxHandler, FindingConfirmedNotificationHandler>();
builder.Services.AddScoped<IOutboxHandler, DisputeOpenedNotificationHandler>();
builder.Services.AddScoped<IOutboxHandler, DisputeResolvedNotificationHandler>();
builder.Services.AddScoped<IOutboxHandler, DisputeAppealedNotificationHandler>();
builder.Services.AddScoped<IOutboxHandler, ArbitrationCompensatedNotificationHandler>();

builder.Services.AddHostedService<OutboxClaimService>();
builder.Services.AddHostedService<NotificationDeliveryService>();
builder.Services.AddHostedService<ParticipationDeadlineService>();
builder.Services.AddHostedService<ReviewDeadlineService>();

var host = builder.Build();

await host.RunAsync();
