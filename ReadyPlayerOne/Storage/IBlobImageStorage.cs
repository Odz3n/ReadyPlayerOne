namespace ReadyPlayerOne.Storage
{
    public interface IBlobImageStorage
    {
        Task<string> UploadAsync(string containerName, string prefix, IFormFile file, CancellationToken token = default);
        Task<bool> DeleteOwnedAsync(string containerName, string expectedPrefix, string? url, CancellationToken cancellationToken = default);
    }
}
