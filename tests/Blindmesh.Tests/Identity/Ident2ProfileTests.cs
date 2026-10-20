using System.Net;
using System.Text.Json;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Identity;

/// <summary>
/// IDENT-2 Profile: the profile of a contributor is saved with its version,
/// a lost change and a foreign account are refused, and a missing or private
/// profile answers with the same safe 404.
/// </summary>
[Collection(RuntimeCollection.Name)]
public sealed class Ident2ProfileTests
{
    private readonly RuntimeFixture _fixture;

    public Ident2ProfileTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task IDENT2SuccessTheProfileIsSavedAndThePublicProfileShowsOnlyTheAllowedFields()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("alice");

        var profile = await host.Client.GetAsync("/v1/profile", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);

        using var document = await host.ReadJsonAsync(profile);
        var root = document.RootElement;
        var version = root.GetProperty("version").GetInt32();
        var username = $"alice-{Guid.NewGuid():N}"[..20];

        var body = JsonSerializer.Serialize(new
        {
            expected_version = version,
            username,
            display_name = "Alice Example",
            bio = "Independent developer who tests other people's software.",
            timezone = "Europe/Berlin",
            skills = new[] { "csharp", "testing" },
            languages = new[] { "en", "de" },
            is_public = true,
        });

        var saved = await host.SendUnsafeAsync(HttpMethod.Patch, "/v1/profile", body);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using var savedDocument = await host.ReadJsonAsync(saved);
        var savedRoot = savedDocument.RootElement;

        Assert.Equal(version + 1, savedRoot.GetProperty("version").GetInt32());
        Assert.Equal(username, savedRoot.GetProperty("username").GetString());
        Assert.Equal("Alice Example", savedRoot.GetProperty("display_name").GetString());
        Assert.Equal("Europe/Berlin", savedRoot.GetProperty("timezone").GetString());
        Assert.Equal(2, savedRoot.GetProperty("skills").GetArrayLength());
        Assert.Equal(2, savedRoot.GetProperty("languages").GetArrayLength());

        // The committed version is the version of the database.
        var stored = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Users.FirstAsync(item => item.Username == username));

        Assert.Equal(version + 1, stored.Version);
        Assert.Equal("Alice Example", stored.DisplayName);

        // The public profile carries the allowed fields and nothing private.
        var publicProfile = await host.Client.GetAsync($"/v1/people/{username}", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, publicProfile.StatusCode);

        var publicBody = await publicProfile.Content.ReadAsStringAsync(CancellationToken.None);

        using var publicDocument = JsonDocument.Parse(publicBody);
        var publicRoot = publicDocument.RootElement;

        Assert.Equal(username, publicRoot.GetProperty("username").GetString());
        Assert.Equal("Alice Example", publicRoot.GetProperty("display_name").GetString());
        Assert.True(publicRoot.TryGetProperty("joined_at", out _));
        Assert.True(publicRoot.TryGetProperty("version", out _));

        Assert.False(publicRoot.TryGetProperty("email", out _));
        Assert.False(publicRoot.TryGetProperty("github_login", out _));
        Assert.False(publicRoot.TryGetProperty("role", out _));
        Assert.False(publicRoot.TryGetProperty("is_banned", out _));
        Assert.False(publicRoot.TryGetProperty("id", out _));

        Assert.DoesNotContain("blindmesh.test", publicBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", publicBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("session", publicBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IDENT2RejectionAStaleVersionIsRefusedAndNothingChanges()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("bob");

        var profile = await host.Client.GetAsync("/v1/profile", CancellationToken.None);
        using var document = await host.ReadJsonAsync(profile);
        var version = document.RootElement.GetProperty("version").GetInt32();

        var first = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            "/v1/profile",
            JsonSerializer.Serialize(new { expected_version = version, display_name = "First Change" }));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // The same version a second time is a lost change.
        var second = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            "/v1/profile",
            JsonSerializer.Serialize(new { expected_version = version, display_name = "Second Change" }));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        using var conflict = await host.ReadJsonAsync(second);
        Assert.Equal("version_conflict", conflict.RootElement.GetProperty("error").GetProperty("code").GetString());

        var stored = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Users.FirstAsync(item => item.Username == "bob"));

        Assert.Equal("First Change", stored.DisplayName);
        Assert.Equal(version + 1, stored.Version);
    }

    [Fact]
    public async Task IDENT2RejectionTheProfileOfAnotherAccountIsForbidden()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("cara");

        var other = await host.FindUserBySubjectAsync("1001");
        Assert.NotNull(other);

        var body = JsonSerializer.Serialize(new
        {
            expected_version = 1,
            user_id = other!.Id,
            display_name = "Not Mine",
        });

        var refused = await host.SendUnsafeAsync(HttpMethod.Patch, "/v1/profile", body);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var document = await host.ReadJsonAsync(refused);
        Assert.Equal("forbidden", document.RootElement.GetProperty("error").GetProperty("code").GetString());

        var stored = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Users.FirstAsync(item => item.Id == other.Id));

        Assert.NotEqual("Not Mine", stored.DisplayName);
    }

    [Fact]
    public async Task IDENT2RejectionAnInvalidFieldIsRefusedWithTheFieldError()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("dana");

        var profile = await host.Client.GetAsync("/v1/profile", CancellationToken.None);
        using var document = await host.ReadJsonAsync(profile);
        var version = document.RootElement.GetProperty("version").GetInt32();

        var body = JsonSerializer.Serialize(new
        {
            expected_version = version,
            username = "not a valid username",
            display_name = "",
            timezone = "Mars/Olympus_Mons",
        });

        var refused = await host.SendUnsafeAsync(HttpMethod.Patch, "/v1/profile", body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);

        using var refusedDocument = await host.ReadJsonAsync(refused);
        var fieldErrors = refusedDocument.RootElement.GetProperty("error").GetProperty("field_errors");

        Assert.True(fieldErrors.TryGetProperty("username", out _));
        Assert.True(fieldErrors.TryGetProperty("display_name", out _));
        Assert.True(fieldErrors.TryGetProperty("timezone", out _));

        var stored = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Users.FirstAsync(item => item.Username == "dana"));

        Assert.Equal(version, stored.Version);
    }

    [Fact]
    public async Task IDENT2BoundaryAMissingAndAPrivateProfileAnswerTheSameSafe404()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("erin");

        var missing = await host.Client.GetAsync("/v1/people/nobody-has-this-name", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var missingBody = await missing.Content.ReadAsStringAsync(CancellationToken.None);

        using var missingDocument = JsonDocument.Parse(missingBody);
        Assert.Equal("not_found", missingDocument.RootElement.GetProperty("error").GetProperty("code").GetString());

        // A private profile of an existing contributor answers in the same way.
        var profile = await host.Client.GetAsync("/v1/profile", CancellationToken.None);
        using var document = await host.ReadJsonAsync(profile);
        var version = document.RootElement.GetProperty("version").GetInt32();

        var hidden = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            "/v1/profile",
            JsonSerializer.Serialize(new { expected_version = version, is_public = false }));

        Assert.Equal(HttpStatusCode.OK, hidden.StatusCode);

        var privateProfile = await host.Client.GetAsync("/v1/people/erin", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, privateProfile.StatusCode);

        var privateBody = await privateProfile.Content.ReadAsStringAsync(CancellationToken.None);

        using var privateDocument = JsonDocument.Parse(privateBody);

        // The answer of a private profile is the same answer as the answer of
        // a profile that does not exist.
        Assert.Equal(
            missingDocument.RootElement.GetProperty("error").GetProperty("code").GetString(),
            privateDocument.RootElement.GetProperty("error").GetProperty("code").GetString());

        Assert.Equal(
            missingDocument.RootElement.GetProperty("error").GetProperty("message").GetString(),
            privateDocument.RootElement.GetProperty("error").GetProperty("message").GetString());

        Assert.DoesNotContain("erin", privateBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("blindmesh.test", privateBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("administrator", privateBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IDENT2TheProfileOfAVisitorWithoutASessionIsRefused()
    {
        using var host = new IdentityTestHost(_fixture);

        using var anonymous = host.CreateClientWithoutCookies();

        var profile = await anonymous.GetAsync("/v1/profile", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);

        var body = await profile.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("blindmesh.test", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IDENT2TheUsernameIsUniqueAndCaseFolded()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("alice");

        var profile = await host.Client.GetAsync("/v1/profile", CancellationToken.None);
        using var document = await host.ReadJsonAsync(profile);
        var version = document.RootElement.GetProperty("version").GetInt32();

        var username = $"unique-{Guid.NewGuid():N}"[..20];

        var first = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            "/v1/profile",
            JsonSerializer.Serialize(new { expected_version = version, username }));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // The same username in another spelling belongs to the same profile.
        var publicProfile = await host.Client.GetAsync($"/v1/people/{username.ToUpperInvariant()}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, publicProfile.StatusCode);

        var stored = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Users.CountAsync(item => item.Username == username.ToLowerInvariant()));

        Assert.Equal(1, stored);
    }
}
