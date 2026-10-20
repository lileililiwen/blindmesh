using System.Net;
using System.Text.Json;
using Blindmesh.Domain.Capabilities;
using Blindmesh.Tests.Identity;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Capabilities;

/// <summary>
/// CAP-1 Declaration: a contributor declares a capability, the declaration is
/// visible in the directory, another contributor may not read or change a
/// private declaration, a credential is refused without a store and without an
/// echo, and a withdrawn declaration keeps its row and its history.
/// </summary>
[Collection(CapabilityCollection.Name)]
public sealed class Cap1DeclarationTests
{
    private readonly RuntimeFixture _fixture;

    public Cap1DeclarationTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CAP1SuccessAContributorDeclaresADeviceCapabilityAndTheDirectoryHoldsIt()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("bob");

        var created = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities",
            CapabilityRequest.Body(
                ("kind", "device"),
                ("label", "MacBook Pro 14"),
                ("tags", new[] { "arm64", "sequoia" }),
                ("visibility", "public"),
                ("available", true),
                ("metadata", new Dictionary<string, string>
                {
                    ["os"] = "macos",
                    ["os_version"] = "15.1",
                })));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var createdDocument = await host.ReadJsonAsync(created);
        var root = createdDocument.RootElement;

        Assert.Equal("device", root.GetProperty("kind").GetString());
        Assert.Equal("MacBook Pro 14", root.GetProperty("label").GetString());
        Assert.Equal("public", root.GetProperty("visibility").GetString());
        Assert.Equal("declared", root.GetProperty("status").GetString());
        Assert.Equal("self_declared", root.GetProperty("verification").GetString());
        Assert.True(root.GetProperty("available").GetBoolean());
        Assert.True(root.GetProperty("usable_for_matching").GetBoolean());
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal("bob", root.GetProperty("owner_username").GetString());

        var tags = root.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString() ?? string.Empty).ToArray();
        Assert.Equal(["arm64", "sequoia"], tags);

        var metadata = root.GetProperty("metadata");
        Assert.Equal("macos", metadata.GetProperty("os").GetString());
        Assert.Equal("15.1", metadata.GetProperty("os_version").GetString());

        // The committed rows are in the database.
        var capabilityId = root.GetProperty("id").GetGuid();

        var rows = await host.WithDatabaseAsync(async dbContext => new
        {
            Capabilities = await dbContext.Capabilities.CountAsync(item => item.Id == capabilityId),
            Tags = await dbContext.CapabilityTags.CountAsync(item => item.CapabilityId == capabilityId),
            History = await dbContext.CapabilityStatusChanges.CountAsync(item => item.CapabilityId == capabilityId),
            Verification = await dbContext.Capabilities
                .Where(item => item.Id == capabilityId)
                .Select(item => item.Verification)
                .FirstAsync(),
        });

        Assert.Equal(1, rows.Capabilities);
        Assert.Equal(2, rows.Tags);
        Assert.Equal(0, rows.History);
        Assert.Equal(CapabilityVerification.SelfDeclared, rows.Verification);

        // A visitor without a session reads the public declaration in the
        // directory.
        var anonymous = host.CreateClientWithoutCookies();
        var directory = await anonymous.GetAsync("/v1/capabilities", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, directory.StatusCode);

        using var directoryDocument = await host.ReadJsonAsync(directory);
        var items = directoryDocument.RootElement.GetProperty("items").EnumerateArray().ToArray();

        Assert.Contains(items, item => item.GetProperty("id").GetGuid() == capabilityId);

        // The owner edits the declaration. The tags are replaced by the new
        // ones and the version moves on.
        var changed = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            $"/v1/capabilities/{capabilityId}",
            CapabilityRequest.Body(
                ("label", "MacBook Pro 16"),
                ("tags", new[] { "arm64" }),
                ("expected_version", 1)));

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        using var changedDocument = await host.ReadJsonAsync(changed);
        var changedRoot = changedDocument.RootElement;

        Assert.Equal("MacBook Pro 16", changedRoot.GetProperty("label").GetString());
        Assert.Equal(2, changedRoot.GetProperty("version").GetInt32());
        Assert.Equal("self_declared", changedRoot.GetProperty("verification").GetString());

        var changedTags = changedRoot.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString() ?? string.Empty).ToArray();
        Assert.Equal(["arm64"], changedTags);

        var tagRows = await host.WithDatabaseAsync(async dbContext => await dbContext.CapabilityTags
            .Where(tag => tag.CapabilityId == capabilityId)
            .Select(tag => tag.Tag)
            .ToListAsync());

        Assert.Equal(["arm64"], tagRows);
    }

    [Fact]
    public async Task CAP1RejectionAContributorMayNotReadOrChangeAPrivateDeclarationOfAnotherContributor()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("cara");

        var created = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities",
            CapabilityRequest.Body(
                ("kind", "skill"),
                ("label", "Fuzzing"),
                ("visibility", "private")));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var createdDocument = await host.ReadJsonAsync(created);
        var capabilityId = createdDocument.RootElement.GetProperty("id").GetGuid();

        // The owner reads the declaration.
        var own = await host.Client.GetAsync($"/v1/capabilities/{capabilityId}", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);

        // Another contributor does not see the declaration at all.
        await host.SignInAsync("bob");

        var hidden = await host.Client.GetAsync($"/v1/capabilities/{capabilityId}", CancellationToken.None);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        var hiddenChange = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            $"/v1/capabilities/{capabilityId}",
            CapabilityRequest.Body(("label", "Taken over"), ("expected_version", 1)));

        Assert.Equal(HttpStatusCode.NotFound, hiddenChange.StatusCode);

        // The declaration of cara is still hers and unchanged.
        var rows = await host.WithDatabaseAsync(async dbContext => await dbContext.Capabilities
            .Where(item => item.Id == capabilityId)
            .Select(item => new { item.Label, item.Version })
            .FirstAsync());

        Assert.Equal("Fuzzing", rows.Label);
        Assert.Equal(1, rows.Version);

        // A public declaration of another contributor is visible, but it may
        // not be changed.
        await host.SignInAsync("alice");

        var publicCapability = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities",
            CapabilityRequest.Body(
                ("kind", "region"),
                ("label", "Berlin"),
                ("visibility", "public")));

        Assert.Equal(HttpStatusCode.Created, publicCapability.StatusCode);

        using var publicDocument = await host.ReadJsonAsync(publicCapability);
        var publicId = publicDocument.RootElement.GetProperty("id").GetGuid();

        await host.SignInAsync("bob");

        var visible = await host.Client.GetAsync($"/v1/capabilities/{publicId}", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);

        var refused = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            $"/v1/capabilities/{publicId}",
            CapabilityRequest.Body(("label", "Taken over"), ("expected_version", 1)));

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var refusedDocument = await host.ReadJsonAsync(refused);
        Assert.Equal("forbidden", refusedDocument.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task CAP1RejectionACredentialInADeclarationIsRefusedWithoutAStoreAndWithoutAnEcho()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("bob");

        // A credential in a field of the body is refused before anything is
        // stored, and the value is never echoed back.
        var withCredential = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities",
            CapabilityRequest.Body(
                ("kind", "account"),
                ("label", "A production account"),
                ("password", "hunter2-secret")));

        Assert.Equal(HttpStatusCode.BadRequest, withCredential.StatusCode);

        var credentialBody = await withCredential.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.DoesNotContain("hunter2-secret", credentialBody, StringComparison.Ordinal);

        // A credential in the metadata is refused as well.
        var withMetadataCredential = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities",
            CapabilityRequest.Body(
                ("kind", "account"),
                ("label", "A production account"),
                ("metadata", new Dictionary<string, string>
                {
                    ["os"] = "macos",
                    ["password"] = "hunter2-secret",
                })));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, withMetadataCredential.StatusCode);

        using var metadataDocument = await host.ReadJsonAsync(withMetadataCredential);
        var error = metadataDocument.RootElement.GetProperty("error");

        Assert.Equal("unprocessable_entity", error.GetProperty("code").GetString());
        Assert.True(error.GetProperty("field_errors").TryGetProperty("metadata", out _));

        var metadataBody = await withMetadataCredential.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.DoesNotContain("hunter2-secret", metadataBody, StringComparison.Ordinal);

        // Nothing was stored.
        var rows = await host.WithDatabaseAsync(async dbContext => new
        {
            Capabilities = await dbContext.Capabilities.CountAsync(item => item.Label == "A production account"),
            Tags = await dbContext.CapabilityTags.CountAsync(item => item.Tag == "hunter2-secret"),
        });

        Assert.Equal(0, rows.Capabilities);
        Assert.Equal(0, rows.Tags);
    }

    [Fact]
    public async Task CAP1BoundaryAWithdrawnDeclarationKeepsItsRowAndItsHistoryAndLeavesTheMatch()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("bob");

        var created = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities",
            CapabilityRequest.Body(
                ("kind", "device"),
                ("label", "ThinkPad X1"),
                ("tags", new[] { "linux" }),
                ("visibility", "public")));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var createdDocument = await host.ReadJsonAsync(created);
        var root = createdDocument.RootElement;
        var capabilityId = root.GetProperty("id").GetGuid();

        // The declaration matches before the withdrawal.
        var before = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities/eligibility",
            CapabilityRequest.Body(("conditions", new[]
            {
                new { kind = "device", value = "ThinkPad X1" },
            })));

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        using var beforeDocument = await host.ReadJsonAsync(before);
        Assert.True(beforeDocument.RootElement.GetProperty("eligible").GetBoolean());

        // The owner withdraws the declaration.
        var withdrawn = await host.SendUnsafeAsync(
            HttpMethod.Post,
            $"/v1/capabilities/{capabilityId}/withdraw",
            CapabilityRequest.Body(("expected_version", 1)));

        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);

        using var withdrawnDocument = await host.ReadJsonAsync(withdrawn);
        var withdrawnRoot = withdrawnDocument.RootElement;

        Assert.Equal("withdrawn", withdrawnRoot.GetProperty("status").GetString());
        Assert.Equal(2, withdrawnRoot.GetProperty("version").GetInt32());
        Assert.False(withdrawnRoot.GetProperty("usable_for_matching").GetBoolean());

        var history = withdrawnRoot.GetProperty("status_history");
        Assert.Equal(1, history.GetArrayLength());
        Assert.Equal("declared", history[0].GetProperty("from_status").GetString());
        Assert.Equal("withdrawn", history[0].GetProperty("to_status").GetString());
        Assert.Equal("bob", history[0].GetProperty("changed_by").GetString());

        // The row, its tags and its history stay in the database.
        var rows = await host.WithDatabaseAsync(async dbContext => new
        {
            Capabilities = await dbContext.Capabilities.CountAsync(item => item.Id == capabilityId),
            Tags = await dbContext.CapabilityTags.CountAsync(item => item.CapabilityId == capabilityId),
            History = await dbContext.CapabilityStatusChanges.CountAsync(item => item.CapabilityId == capabilityId),
        });

        Assert.Equal(1, rows.Capabilities);
        Assert.Equal(1, rows.Tags);
        Assert.Equal(1, rows.History);

        // The withdrawn declaration leaves the match.
        var after = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities/eligibility",
            CapabilityRequest.Body(("conditions", new[]
            {
                new { kind = "device", value = "ThinkPad X1" },
            })));

        Assert.Equal(HttpStatusCode.OK, after.StatusCode);

        using var afterDocument = await host.ReadJsonAsync(after);
        Assert.False(afterDocument.RootElement.GetProperty("eligible").GetBoolean());

        // The withdrawn declaration is not in the directory any more, but the
        // owner still reads it in the own list.
        var directory = await host.Client.GetAsync("/v1/capabilities", CancellationToken.None);
        using var directoryDocument = await host.ReadJsonAsync(directory);

        Assert.DoesNotContain(
            directoryDocument.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == capabilityId);

        var own = await host.Client.GetAsync("/v1/capabilities?scope=own", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);

        using var ownDocument = await host.ReadJsonAsync(own);
        Assert.Contains(
            ownDocument.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == capabilityId);

        // A withdrawn declaration is not offered any more, so another
        // contributor does not read it.
        await host.SignInAsync("cara");

        var hidden = await host.Client.GetAsync($"/v1/capabilities/{capabilityId}", CancellationToken.None);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task CAP1BoundaryAStaleVersionIsRefusedAndTheDeclarationKeepsItsValue()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("bob");

        var created = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/capabilities",
            CapabilityRequest.Body(("kind", "role"), ("label", "tester"), ("visibility", "public")));

        using var createdDocument = await host.ReadJsonAsync(created);
        var capabilityId = createdDocument.RootElement.GetProperty("id").GetGuid();

        var changed = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            $"/v1/capabilities/{capabilityId}",
            CapabilityRequest.Body(("label", "tester and reviewer"), ("expected_version", 1)));

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        // The second change carries the old version and is refused.
        var stale = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            $"/v1/capabilities/{capabilityId}",
            CapabilityRequest.Body(("label", "something else"), ("expected_version", 1)));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        using var staleDocument = await host.ReadJsonAsync(stale);
        Assert.Equal("version_conflict", staleDocument.RootElement.GetProperty("error").GetProperty("code").GetString());

        var rows = await host.WithDatabaseAsync(async dbContext => await dbContext.Capabilities
            .Where(item => item.Id == capabilityId)
            .Select(item => new { item.Label, item.Version })
            .FirstAsync());

        Assert.Equal("tester and reviewer", rows.Label);
        Assert.Equal(2, rows.Version);
    }

    [Fact]
    public async Task CAP1RejectionAContributorWithoutASessionMayNotDeclareACapability()
    {
        using var host = new IdentityTestHost(_fixture);

        var anonymous = host.CreateClientWithoutCookies();

        var refused = await anonymous.PostAsync(
            "/v1/capabilities",
            new StringContent(
                CapabilityRequest.Body(("kind", "device"), ("label", "A device")),
                System.Text.Encoding.UTF8,
                "application/json"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        var rows = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Capabilities.CountAsync(item => item.Label == "A device"));

        Assert.Equal(0, rows);
    }
}
