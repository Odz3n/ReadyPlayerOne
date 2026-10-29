namespace ReadyPlayerOne.Storage;

public class ImageValidationException(string message) : Exception(message);

public static class ImageUploadValidator
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public static async Task<(string Extension, string ContentType)> ValidateAsync(
        IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            throw new ImageValidationException("Choose a non-empty image file.");

        if (file.Length > MaxBytes)
            throw new ImageValidationException("Maximum image size is 5 MiB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            throw new ImageValidationException("Allowed image extensions: JPG, JPEG, PNG, WebP.");

        byte[] header = new byte[12];
        await using (var stream = file.OpenReadStream())
        {
            try
            {
                await stream.ReadExactlyAsync(header, cancellationToken);
            }
            catch (EndOfStreamException)
            {
                throw new ImageValidationException("The image file is too short or invalid.");
            }
        }

        string? detectedType = null;
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            detectedType = "image/jpeg";
        else if (header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            detectedType = "image/png";
        else if (header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            detectedType = "image/webp";

        var expectedType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };

        if (detectedType is null || detectedType != expectedType || !string.Equals(file.ContentType, expectedType, StringComparison.OrdinalIgnoreCase))
            throw new ImageValidationException("The file signature, extension and Content-Type must agree.");

        return (extension == ".jpeg" ? ".jpg" : extension, expectedType);
    }
}
