namespace Blindmesh.Application.Findings;

/// <summary>
/// The private object storage of the evidence. The store is scoped, so it
/// shares the scoped database context of the request with the other modules,
/// but every method of the storage is a network call: it never runs inside the
/// transaction of a command.
/// </summary>
public interface IObjectStorage
{
    /// <summary>True when the storage is configured and reachable.</summary>
    bool IsConfigured { get; }

    /// <summary>Creates the bucket of the evidence when it does not exist yet.</summary>
    Task EnsureBucketAsync(CancellationToken cancellationToken);

    /// <summary>Writes the bytes of a file into the private bucket.</summary>
    Task PutAsync(
        string key,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Signs the address of a file for a short time. The address points at the
    /// private bucket and it never carries a credential of the product.
    /// </summary>
    Task<string> CreateSignedUrlAsync(
        string key,
        string fileName,
        string contentType,
        TimeSpan lifetime,
        CancellationToken cancellationToken);
}
