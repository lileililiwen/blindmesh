using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Blindmesh.Application.Identity;
using Blindmesh.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Blindmesh.Infrastructure.Identity;

/// <summary>
/// The GitHub authorization code flow. The API owns the flow: the browser is
/// sent to GitHub, the code is exchanged here and the identity is read with
/// the access token. The access token is used for that one read and is never
/// stored or logged.
/// </summary>
public sealed class GitHubOAuthClient(
    HttpClient httpClient,
    IdentityOptions options,
    ILogger<GitHubOAuthClient> logger) : IGitHubIdentityClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(options.GitHub.ClientId) &&
        !string.IsNullOrWhiteSpace(options.GitHub.ClientSecret);

    public string BuildAuthorizeUrl(string state, string redirectUri, string? localActor)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["client_id"] = options.GitHub.ClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = options.GitHub.Scope,
            ["state"] = state,
            ["response_type"] = "code",
        };

        return $"{options.GitHub.AuthorizeUrl}?{ToQuery(query)}";
    }

    public async Task<ProviderIdentity> ExchangeAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("The GitHub client of the product is not configured.");
        }

        var accessToken = await RequestAccessTokenAsync(code, redirectUri, cancellationToken);

        try
        {
            var user = await ReadUserAsync(accessToken, cancellationToken);
            var email = user.Email;

            if (string.IsNullOrWhiteSpace(email))
            {
                email = await ReadPrimaryEmailAsync(accessToken, cancellationToken);
            }

            logger.LogInformation("Read the identity of a GitHub account {Subject}.", user.Id);

            return new ProviderIdentity(
                IdentityProviders.GitHub,
                user.Id.ToString(),
                user.Login,
                email ?? string.Empty);
        }
        finally
        {
            // The token of the provider leaves the process with this scope.
            accessToken = string.Empty;
        }
    }

    private async Task<string> RequestAccessTokenAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.GitHub.TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["client_id"] = options.GitHub.ClientId,
                ["client_secret"] = options.GitHub.ClientSecret,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
            }),
        };

        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken);

        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new InvalidOperationException("The identity provider did not return an access token.");
        }

        return payload.AccessToken;
    }

    private async Task<GitHubUser> ReadUserAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, options.GitHub.UserUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("Blindmesh");
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<GitHubUser>(JsonOptions, cancellationToken);

        return user ?? throw new InvalidOperationException("The identity provider did not return an account.");
    }

    private async Task<string?> ReadPrimaryEmailAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, options.GitHub.UserEmailsUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("Blindmesh");
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var emails = await response.Content.ReadFromJsonAsync<List<GitHubEmail>>(JsonOptions, cancellationToken);

        return emails?
            .Where(email => email.Primary && email.Verified)
            .Select(email => email.Email)
            .FirstOrDefault();
    }

    private static string ToQuery(IReadOnlyDictionary<string, string> values) =>
        string.Join(
            "&",
            values.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("scope")] string? Scope);

    private sealed record GitHubUser(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("login")] string Login,
        [property: JsonPropertyName("email")] string? Email);

    private sealed record GitHubEmail(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("primary")] bool Primary,
        [property: JsonPropertyName("verified")] bool Verified);
}
