using System.Security.Cryptography;
using System.Text;

namespace Blindmesh.Application.Identity;

/// <summary>
/// The opaque values of the session and of the OAuth state. The raw values
/// leave the process only in a cookie or in a redirect; the database holds
/// their hash.
/// </summary>
public static class SecretToken
{
    /// <summary>The size of a session token in bytes.</summary>
    public const int SessionTokenBytes = 32;

    /// <summary>Creates a random opaque value.</summary>
    public static string Create(int bytes = SessionTokenBytes) =>
        Base64Url(RandomNumberGenerator.GetBytes(bytes));

    /// <summary>The hash that is stored instead of the raw value.</summary>
    public static string Hash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    /// <summary>Compares two secrets without leaking their relation through time.</summary>
    public static bool Matches(string hash, string candidate) =>
        CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(hash),
            Convert.FromHexString(Hash(candidate)));

    /// <summary>
    /// The CSRF token of a session. It is derived from the session token, so
    /// it is bound to the server session and cannot be guessed without it. The
    /// browser never has to read the session cookie, because the API hands the
    /// token out with the session.
    /// </summary>
    public static string DeriveCsrfToken(string sessionToken)
    {
        ArgumentNullException.ThrowIfNull(sessionToken);

        var mac = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(sessionToken),
            "blindmesh-csrf-v1"u8);

        return Base64Url(mac);
    }

    /// <summary>True when the candidate is the CSRF token of the session.</summary>
    public static bool MatchesCsrfToken(string sessionToken, string? candidate)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        var expected = DeriveCsrfToken(sessionToken);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(candidate));
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
