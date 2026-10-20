using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Blindmesh.Application.Configuration;
using Blindmesh.Application.Findings;
using Microsoft.Extensions.Options;

namespace Blindmesh.Infrastructure.Storage;

/// <summary>
/// The private object storage of the evidence. It speaks the S3 API of the
/// local RustFS of the compose stack and of any other S3 compatible service.
/// The bucket is private: a file is only reachable with a signed address that
/// lives for a short time, and the address never carries a credential of the
/// product.
/// </summary>
public sealed class S3ObjectStorage : IObjectStorage
{
    private readonly ObjectStorageOptions _options;
    private readonly Lazy<IAmazonS3> _client;
    private readonly Lazy<IAmazonS3> _signingClient;

    public S3ObjectStorage(IOptions<BlindmeshRuntimeOptions> options)
    {
        _options = options.Value.ObjectStorage;
        _client = new Lazy<IAmazonS3>(() => CreateClient(_options.Endpoint), LazyThreadSafetyMode.ExecutionAndPublication);
        _signingClient = new Lazy<IAmazonS3>(
            () => CreateClient(PublicEndpoint), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// The address the browser uses. It is the address of the service when the
    /// browser reaches it at the same place as the product.
    /// </summary>
    public string PublicEndpoint => string.IsNullOrWhiteSpace(_options.PublicEndpoint)
        ? _options.Endpoint
        : _options.PublicEndpoint;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Endpoint);

    public async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return;
        }

        try
        {
            await _client.Value.PutBucketAsync(
                new PutBucketRequest { BucketName = _options.Bucket, UseClientRegion = true },
                cancellationToken);
        }
        catch (AmazonServiceException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            // The bucket exists already, which is the normal case.
        }
    }

    public async Task PutAsync(
        string key,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(content.ToArray(), writable: false);

        await _client.Value.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
                AutoCloseStream = false,
            },
            cancellationToken);
    }

    public Task<string> CreateSignedUrlAsync(
        string key,
        string fileName,
        string contentType,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Expires = DateTime.UtcNow.Add(lifetime),
            Protocol = Protocol.HTTP,
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentType = contentType,
                ContentDisposition = $"attachment; filename=\"{SafeFileName(fileName)}\"",
            },
        };

        // The signature covers the host of the address, so the address is signed
        // with the endpoint the browser uses.
        return Task.FromResult(_signingClient.Value.GetPreSignedURL(request));
    }

    private IAmazonS3 CreateClient(string endpoint)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = _options.Region,
            UseHttp = endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
            Timeout = TimeSpan.FromSeconds(60),
        };

        return new AmazonS3Client(
            new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
            config);
    }

    /// <summary>
    /// The name of a file in the header of a download never carries a path and
    /// never carries a quote.
    /// </summary>
    private static string SafeFileName(string fileName)
    {
        var safe = Path.GetFileName(fileName).Replace("\"", string.Empty, StringComparison.Ordinal);

        return string.IsNullOrWhiteSpace(safe) ? "evidence" : safe;
    }
}
