using System.Text.Json;
using System.Text.Json.Serialization;
using Blindmesh.Api.Configuration;
using Blindmesh.Api.Endpoints;
using Blindmesh.Api.Endpoints.Capabilities;
using Blindmesh.Api.Endpoints.Credits;
using Blindmesh.Api.Endpoints.Identity;
using Blindmesh.Api.Endpoints.Projects;
using Blindmesh.Api.Infrastructure;
using Blindmesh.Application.Runtime;
using Blindmesh.Infrastructure;
using Blindmesh.Infrastructure.Capabilities;
using Blindmesh.Infrastructure.Credits;
using Blindmesh.Infrastructure.Identity;
using Blindmesh.Infrastructure.Projects;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// A scoped service must never be captured by a longer lived one. The check is
// always on so a mis-registration fails at startup instead of at runtime.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

var apiOptions = builder.Configuration.GetSection(ApiOptions.SectionName).Get<ApiOptions>() ?? new ApiOptions();
var appEnvironment = builder.Configuration["APP_ENV"] ?? builder.Environment.EnvironmentName;

// A production host must never start with a test fixture enabled.
RuntimePolicy.Validate(appEnvironment, apiOptions.TestAuthenticationEnabled);

builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.SectionName));
builder.Services.AddBlindmeshInfrastructure(builder.Configuration);
builder.Services.AddBlindmeshIdentity(builder.Configuration);
builder.Services.AddBlindmeshProjects();
builder.Services.AddBlindmeshCapabilities();
builder.Services.AddBlindmeshCredits();
builder.Services.AddScoped<RuntimeHeartbeatService>();
builder.Services.AddScoped<IdentitySessionGuard>();
builder.Services.AddHostedService<RuntimePolicyHostedService>();
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        // The served document must not depend on the host it was generated on.
        document.Info.Title = "Blindmesh API";
        document.Info.Version = "v1";
        document.Servers =
        [
            new OpenApiServer { Url = "/v1" },
            new OpenApiServer { Url = "/api/v1" },
        ];

        return Task.CompletedTask;
    });
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<OriginGuardMiddleware>();

app.MapHealthEndpoints();
app.MapRuntimeEndpoints();
app.MapIdentityEndpoints();
app.MapProjectEndpoints();
app.MapCapabilityEndpoints();
app.MapCreditEndpoints();
app.MapLocalGitHubFixtureEndpoints();
app.MapOpenApi("/v1/openapi/{documentName}.json");

if (await CliCommandRunner.TryRunAsync(args, app))
{
    return;
}

app.Run();

/// <summary>Exposed so the integration tests can host the real API.</summary>
public partial class Program;
