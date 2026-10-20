using Blindmesh.Application.Findings;

namespace Blindmesh.Tests.Findings;

/// <summary>
/// A stand in for the private object storage. It keeps the bytes of the files
/// of the evidence in the memory of the test run and it signs an address that
/// points at the stand in itself. The real client of the S3 API is proven by
/// the browser journey against the local stack.
/// </summary>
public sealed class FakeObjectStorage : IObjectStorage
{
    private readonly Dictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

    public bool IsConfigured => true;

    public IReadOnlyDictionary<string, byte[]> Objects => _objects;

    public Task EnsureBucketAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task PutAsync(
        string key,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken)
    {
        _objects[key] = content.ToArray();

        return Task.CompletedTask;
    }

    public Task<string> CreateSignedUrlAsync(
        string key,
        string fileName,
        string contentType,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var url = $"https://evidence.invalid/{Uri.EscapeDataString(key)}" +
            $"?file={Uri.EscapeDataString(fileName)}&expires={lifetime.TotalSeconds:0}";

        return Task.FromResult(url);
    }
}
