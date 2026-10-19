using System.Security.Cryptography;
using System.Text;

namespace Blindmesh.Application.Runtime;

/// <summary>
/// Canonical payload hash used to detect a replayed key that carries a different
/// payload. The hash covers the exact request bytes, so any change to the
/// payload changes the hash.
/// </summary>
public static class PayloadHasher
{
    public static string Compute(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
