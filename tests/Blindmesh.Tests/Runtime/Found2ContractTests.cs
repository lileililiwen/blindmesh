using System.Net;
using System.Text;
using System.Text.Json;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Runtime;

/// <summary>
/// FOUND-2 Contracts: the error envelope, the request id, the origin guard, the
/// scoped idempotency records and the versioned OpenAPI document.
/// </summary>
[Collection(RuntimeCollection.Name)]
public sealed class Found2ContractTests
{
    private readonly RuntimeFixture _fixture;

    public Found2ContractTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    private BlindmeshApiFactory CreateFactory()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Blindmesh:Database:ConnectionString"] = _fixture.ConnectionString,
            ["Blindmesh:ObjectStorage:Endpoint"] = _fixture.Dependencies.Endpoint,
            ["Blindmesh:ObjectStorage:HealthPath"] = "/minio/health/live",
            ["Blindmesh:Mail:Host"] = "127.0.0.1",
            ["Blindmesh:Mail:Port"] = _fixture.Dependencies.Port.ToString(),
            ["Api:AllowedOrigins:0"] = "http://localhost:3000",
        };

        return new BlindmeshApiFactory(settings);
    }

    private static HttpRequestMessage HeartbeatRequest(
        string? idempotencyKey = "found2-heartbeat-key-0001",
        string? origin = null,
        string body = "{\"note\":\"found2\"}")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/runtime/heartbeat")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        if (origin is not null)
        {
            request.Headers.TryAddWithoutValidation("Origin", origin);
        }

        return request;
    }

    private async Task<long> CountOutboxMessagesAsync()
    {
        var options = new DbContextOptionsBuilder<BlindmeshDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        await using var dbContext = new BlindmeshDbContext(options);
        return await dbContext.OutboxMessages.CountAsync();
    }

    [Fact]
    public async Task AMissingIdempotencyKeyUsesTheArchitectureErrorEnvelope()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(HeartbeatRequest(idempotencyKey: null));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(body);
        var error = document.RootElement.GetProperty("error");

        Assert.Equal("invalid_idempotency_key", error.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("message").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("request_id").GetString()));
        Assert.True(error.TryGetProperty("field_errors", out _));

        Assert.DoesNotContain("SELECT", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheRequestIdIsEchoedOnTheResponseAndInTheErrorEnvelope()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var request = HeartbeatRequest(idempotencyKey: null);
        request.Headers.TryAddWithoutValidation("X-Request-Id", "found2-request-id-1");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("found2-request-id-1", response.Headers.GetValues("X-Request-Id").Single());

        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            "found2-request-id-1",
            document.RootElement.GetProperty("error").GetProperty("request_id").GetString());
    }

    [Fact]
    public async Task AMismatchedOriginIsRejectedBeforeAnySideEffect()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var before = await CountOutboxMessagesAsync();

        using var response = await client.SendAsync(
            HeartbeatRequest(origin: "https://not-allowed.example", idempotencyKey: "found2-origin-key-0001"));

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var document = JsonDocument.Parse(body);
        Assert.Equal("forbidden", document.RootElement.GetProperty("error").GetProperty("code").GetString());

        Assert.Equal(before, await CountOutboxMessagesAsync());
    }

    [Fact]
    public async Task TheSameKeyAndPayloadReplaysTheOriginalResponseAndWritesOneEvent()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var before = await CountOutboxMessagesAsync();

        using var first = await client.SendAsync(HeartbeatRequest(idempotencyKey: "found2-replay-key-0001"));
        var firstBody = await first.Content.ReadAsStringAsync();

        using var second = await client.SendAsync(HeartbeatRequest(idempotencyKey: "found2-replay-key-0001"));
        var secondBody = await second.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(firstBody, secondBody);
        Assert.Equal(before + 1, await CountOutboxMessagesAsync());
    }

    [Fact]
    public async Task TheSameKeyWithAnotherPayloadIsRejected()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var first = await client.SendAsync(HeartbeatRequest(idempotencyKey: "found2-payload-key-0001", body: "{\"note\":\"first\"}"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await client.SendAsync(HeartbeatRequest(idempotencyKey: "found2-payload-key-0001", body: "{\"note\":\"second\"}"));
        var body = await second.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            "idempotency_key_reuse",
            document.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TheOpenApiDocumentIsVersionedAndStable()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var first = await client.GetAsync("/v1/openapi/v1.json");
        var firstBody = await first.Content.ReadAsStringAsync();

        using var second = await client.GetAsync("/v1/openapi/v1.json");
        var secondBody = await second.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(firstBody, secondBody);

        using var document = JsonDocument.Parse(firstBody);
        var root = document.RootElement;

        Assert.StartsWith("3.1", root.GetProperty("openapi").GetString()!, StringComparison.Ordinal);
        Assert.Equal("v1", root.GetProperty("info").GetProperty("version").GetString());

        var paths = root.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/v1/health/live", out _));
        Assert.True(paths.TryGetProperty("/v1/health/ready", out _));
        Assert.True(paths.TryGetProperty("/v1/runtime/heartbeat", out _));

        var heartbeat = paths.GetProperty("/v1/runtime/heartbeat").GetProperty("post");
        Assert.True(heartbeat.GetProperty("responses").TryGetProperty("201", out _));
        Assert.True(heartbeat.GetProperty("responses").TryGetProperty("409", out _));
    }

    [Fact]
    public async Task TheResponseUsesSnakeCaseJsonAndUtf8Timestamps()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/v1/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);

        Assert.Equal("available", document.RootElement.GetProperty("status").GetString());
        Assert.EndsWith("+00:00", document.RootElement.GetProperty("checked_at").GetString()!, StringComparison.Ordinal);
        Assert.Contains("\"checked_at\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"checkedAt\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheContentTypeIsJsonAndTheRequestIdHeaderIsAlwaysPresent()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/v1/health/live");

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.Contains("X-Request-Id"), "The request id header is missing.");
    }
}
