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
    public string Endpoint { get; init; } = string.Empty;

    public string HealthPath { get; init; } = "/minio/health/live";
}

public sealed class MailOptions
{
    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 1025;
}
