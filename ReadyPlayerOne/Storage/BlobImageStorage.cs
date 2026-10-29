using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ReadyPlayerOne.Storage;

public class BlobImageStorage : IBlobImageStorage
{
    public const string AvatarContainer = "avatars";
    public const string GameCoverContainer = "game-covers";

    private readonly BlobServiceClient _serviceClient;

    public BlobImageStorage(
        BlobServiceClient serviceClient)
    {
        _serviceClient = serviceClient;
    }

    public async Task<string> UploadAsync(string containerName, string prefix, IFormFile file, CancellationToken cancellationToken = default)
    {
        ValidateContainerAndPrefix(containerName, prefix);
        var (extension, contentType) = await ImageUploadValidator.ValidateAsync(file, cancellationToken);

        var blobName = $"{prefix}/{Guid.NewGuid():N}{extension}";
        var blob = _serviceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType,
                CacheControl = "public, max-age=31536000, immutable"
            },
            Conditions = new BlobRequestConditions 
            { 
                IfNoneMatch = ETag.All 
            }
        }, cancellationToken);

        return blob.Uri.AbsoluteUri;
    }

    public async Task<bool> DeleteOwnedAsync(string containerName, string expectedPrefix,
        string? url, CancellationToken cancellationToken = default)
    {
        ValidateContainerAndPrefix(containerName, expectedPrefix);
        if (string.IsNullOrWhiteSpace(url) 
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri) 
            || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            return false;

        var container = _serviceClient.GetBlobContainerClient(containerName);
        var containerUri = container.Uri;
        var basePath = containerUri.AbsolutePath.TrimEnd('/') + "/";

        if (!string.Equals(uri.Scheme, containerUri.Scheme, StringComparison.OrdinalIgnoreCase) 
            || !string.Equals(uri.Host, containerUri.Host, StringComparison.OrdinalIgnoreCase) 
            || uri.Port != containerUri.Port 
            || !uri.AbsolutePath.StartsWith(basePath, StringComparison.Ordinal))
            return false;

        var blobName = Uri.UnescapeDataString(uri.AbsolutePath[basePath.Length..]);
        if (!blobName.StartsWith(expectedPrefix + "/", StringComparison.Ordinal) ||
            blobName.Contains('\\') || blobName.Contains("..", StringComparison.Ordinal))
            return false;

        var result = await container.GetBlobClient(blobName)
            .DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots,
                cancellationToken: cancellationToken);

        return result.Value;
    }

    private static void ValidateContainerAndPrefix(string containerName, string prefix)
    {
        if (containerName is not (AvatarContainer or GameCoverContainer) 
            || string.IsNullOrWhiteSpace(prefix) || prefix.Contains("..", StringComparison.Ordinal) 
            || prefix.StartsWith('/') || prefix.EndsWith('/'))
            throw new ArgumentException("Invalid image container or blob prefix.");
    }
}
