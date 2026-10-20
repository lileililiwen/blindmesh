namespace Blindmesh.Application.Configuration;

/// <summary>
/// Runtime service locations. Every value is configuration, never a compiled-in
/// machine-specific address.
/// </summary>
public sealed class BlindmeshRuntimeOptions
{
    public const string SectionName = "Blindmesh";

    public DatabaseOptions Database { get; init; } = new();

    public ObjectStorageOptions ObjectStorage { get; init; } = new();

    public MailOptions Mail { get; init; } = new();
}

public sealed class DatabaseOptions
{
    public string ConnectionString { get; init; } = string.Empty;
}

public sealed class ObjectStorageOptions
{
    /// <summary>
    /// The address the product uses to reach the private object storage. It is
    /// the address of the service inside the network of the runtime.
    /// </summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>
    /// The address the browser uses to reach the private object storage. It is
    /// empty when the browser reaches the storage at the same address as the
    /// product.
    /// </summary>
    public string PublicEndpoint { get; init; } = string.Empty;

    public string HealthPath { get; init; } = "/minio/health/live";

    /// <summary>The bucket of the private files of the evidence.</summary>
    public string Bucket { get; init; } = "blindmesh-evidence";

    /// <summary>The name of the region of the bucket.</summary>
    public string Region { get; init; } = "us-east-1";

    /// <summary>The access key of the private object storage.</summary>
    public string AccessKey { get; init; } = string.Empty;

    /// <summary>The secret key of the private object storage.</summary>
    public string SecretKey { get; init; } = string.Empty;
}

public sealed class MailOptions
{
    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 1025;
}
