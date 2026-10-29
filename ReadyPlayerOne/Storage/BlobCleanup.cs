namespace ReadyPlayerOne.Storage
{
    public static class BlobCleanup
    {
        public static async Task TryDeleteAsync(IBlobImageStorage storage, ILogger logger, string container, string prefix, string? url)
        {
            try
            {
                await storage.DeleteOwnedAsync(container, prefix, url, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, $"Could not clean up the old blob for {prefix}");
            }
        }
    }
}
