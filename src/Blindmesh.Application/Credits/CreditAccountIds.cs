using System.Security.Cryptography;
using System.Text;

using Blindmesh.Domain.Credits;

namespace Blindmesh.Application.Credits;

/// <summary>
/// The identity of an account of the ledger. The identifier is derived from
/// the bucket, the owner and the reference, so the same account is never
/// created twice and the order of the identifiers is the same everywhere.
/// </summary>
public static class CreditAccountIds
{
    /// <summary>The derived identifier of the account of the key.</summary>
    public static Guid Derive(LedgerBucket bucket, Guid? ownerId, string reference)
    {
        var owner = ownerId.HasValue ? ownerId.Value.ToString("D") : "platform";
        var key = $"{bucket}|{owner}|{reference}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var bytes = hash[..16];

        // The version and the variant bits keep the identifier a well formed
        // uuid, so it is stored and read like every other one.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }

    /// <summary>
    /// The order of the identifiers of the database. The order of the bytes of
    /// an uuid is the order the database uses when it locks a set of rows.
    /// </summary>
    public static string Order(Guid accountId) => accountId.ToString("N");
}
