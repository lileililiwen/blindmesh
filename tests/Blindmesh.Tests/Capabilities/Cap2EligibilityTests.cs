using System.Net;
using System.Text.Json;
using Blindmesh.Tests.Identity;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Capabilities;

/// <summary>
/// CAP-2 Eligibility: the evaluation of the hard conditions is deterministic,
/// it explains every unmet condition without disclosing a private declaration,
/// it reads the committed state and not a cached preview, and it never invents
/// a task that the marketplace has not published.
/// </summary>
[Collection(CapabilityCollection.Name)]
public sealed class Cap2EligibilityTests
{
    private readonly RuntimeFixture _fixture;

    public Cap2EligibilityTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CAP2SuccessAnEligibleContributorMeetsEveryHardCondition()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("bob");

        await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities",
            CapabilityRequest.Body(
                ("kind", "region"),
                ("label", "Berlin"),
                ("tags", new[] { "europe" }),
                ("visibility", "public")));

        await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities",
            CapabilityRequest.Body(
                ("kind", "device"),
                ("label", "MacBook Pro 14"),
                ("visibility", "public")));

        await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities",
            CapabilityRequest.Body(
                ("kind", "role"),
                ("label", "tester"),
                ("visibility", "public")));

        var evaluation = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities/eligibility",
            CapabilityRequest.Body(("conditions", new[]
            {
                new { kind = "region", value = "Berlin" },
                new { kind = "device", value = "macbook pro 14" },
                new { kind = "role", value = "tester" },
            })));

        Assert.Equal(HttpStatusCode.OK, evaluation.StatusCode);

        using var document = await host.ReadJsonAsync(evaluation);
        var root = document.RootElement;

        Assert.True(root.GetProperty("eligible").GetBoolean());

        var conditions = root.GetProperty("conditions").EnumerateArray().ToArray();
        Assert.Equal(3, conditions.Length);

        foreach (var condition in conditions)
        {
            Assert.True(condition.GetProperty("satisfied").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(condition.GetProperty("reason").GetString()));
        }

        Assert.Equal("region", conditions[0].GetProperty("kind").GetString());
        Assert.Equal("Berlin", conditions[0].GetProperty("value").GetString());

        // The same state and the same conditions always give the same answer.
        var again = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities/eligibility",
            CapabilityRequest.Body(("conditions", new[]
            {
                new { kind = "region", value = "Berlin" },
                new { kind = "device", value = "macbook pro 14" },
                new { kind = "role", value = "tester" },
            })));

        var first = await evaluation.Content.ReadAsStringAsync(CancellationToken.None);
        var second = await again.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task CAP2RejectionAContributorWithoutTheRequiredRegionGetsAnActionableReason()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("cara");

        // Cara declares a device and a private region. A private declaration is
        // never used for a match, so it can not satisfy the condition.
        await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities",
            CapabilityRequest.Body(
                ("kind", "device"),
                ("label", "Surface Laptop"),
                ("visibility", "public")));

        await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities",
            CapabilityRequest.Body(
                ("kind", "region"),
                ("label", "Paris"),
                ("visibility", "private")));

        var evaluation = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities/eligibility",
            CapabilityRequest.Body(("conditions", new[]
            {
                new { kind = "region", value = "Paris" },
                new { kind = "device", value = "Surface Laptop" },
            })));

        Assert.Equal(HttpStatusCode.OK, evaluation.StatusCode);

        using var document = await host.ReadJsonAsync(evaluation);
        var root = document.RootElement;

        Assert.False(root.GetProperty("eligible").GetBoolean());

        var conditions = root.GetProperty("conditions").EnumerateArray().ToArray();
        Assert.Equal(2, conditions.Length);

        var region = conditions[0];
        var device = conditions[1];

        Assert.False(region.GetProperty("satisfied").GetBoolean());
        Assert.True(device.GetProperty("satisfied").GetBoolean());

        var reason = region.GetProperty("reason").GetString()!;

        Assert.Contains("region", reason, StringComparison.Ordinal);
        Assert.Contains("Paris", reason, StringComparison.Ordinal);

        // The reason never names the private declaration of the contributor.
        Assert.DoesNotContain("private", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, root.GetProperty("matchable_declarations").GetInt32());
    }

    [Fact]
    public async Task CAP2BoundaryTheEvaluationUsesTheCommittedStateAndNotACachedPreview()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("bob");

        var created = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities",
            CapabilityRequest.Body(
                ("kind", "device"),
                ("label", "Framework Laptop"),
                ("visibility", "public")));

        using var createdDocument = await host.ReadJsonAsync(created);
        var capabilityId = createdDocument.RootElement.GetProperty("id").GetGuid();

        var preview = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities/eligibility",
            CapabilityRequest.Body(("conditions", new[]
            {
                new { kind = "device", value = "Framework Laptop" },
            })));

        using var previewDocument = await host.ReadJsonAsync(preview);
        Assert.True(previewDocument.RootElement.GetProperty("eligible").GetBoolean());

        // The declaration changes after the preview.
        var changed = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            $"/v1/capabilities/{capabilityId}",
            CapabilityRequest.Body(("available", false), ("expected_version", 1)));

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        // The evaluation reads the committed state and does not trust the
        // answer of the preview.
        var rechecked = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities/eligibility",
            CapabilityRequest.Body(("conditions", new[]
            {
                new { kind = "device", value = "Framework Laptop" },
            })));

        Assert.Equal(HttpStatusCode.OK, rechecked.StatusCode);

        using var recheckedDocument = await host.ReadJsonAsync(rechecked);
        Assert.False(recheckedDocument.RootElement.GetProperty("eligible").GetBoolean());

        var reason = recheckedDocument.RootElement
            .GetProperty("conditions")[0]
            .GetProperty("reason")
            .GetString()!;

        Assert.Contains("no public and available device declaration", reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CAP2RejectionAConditionWithAnUnknownKindIsRefused()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("bob");

        var refused = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities/eligibility",
            CapabilityRequest.Body(("conditions", new[]
            {
                new { kind = "skill", value = "Fuzzing" },
            })));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);

        using var document = await host.ReadJsonAsync(refused);
        Assert.Equal(
            "unprocessable_entity",
            document.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task CAP2BoundaryTheEvaluationOfATaskIsNotFoundBecauseNoTaskIsPublished()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("bob");

        var taskId = Guid.NewGuid();

        var refused = await host.Client.GetAsync(
            $"/v1/capabilities/tasks/{taskId}/eligibility",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);

        using var document = await host.ReadJsonAsync(refused);
        var error = document.RootElement.GetProperty("error");

        Assert.Equal("not_found", error.GetProperty("code").GetString());

        // The answer never invents a task and never holds a condition.
        var body = await refused.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.DoesNotContain("conditions", body, StringComparison.Ordinal);
        Assert.DoesNotContain(taskId.ToString(), body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CAP2RejectionAContributorWithoutASessionMayNotEvaluateAnEligibility()
    {
        using var host = new IdentityTestHost(_fixture);

        var anonymous = host.CreateClientWithoutCookies();

        var refused = await anonymous.GetAsync(
            $"/v1/capabilities/tasks/{Guid.NewGuid()}/eligibility",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }
}
