namespace CfiApp.Application.Abstractions;

public sealed record StoredFile(string StorageKey, long ByteSize, string Sha256Hex);

/// <summary>
/// Where uploaded files actually live. Local disk today, object storage later - callers
/// never see the difference, because nothing above this interface knows a file path exists.
/// </summary>
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string suggestedExtension, CancellationToken cancellationToken);

    /// <summary>Opens a previously saved file for reading. Null if the storage key does not exist.</summary>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
}

public static class UploadPolicy
{
    /// <summary>
    /// Deliberately conservative: a phone photo is typically 3-8 MB, and the site raises
    /// dozens of reports a day - unbounded uploads is how a disk quietly fills up.
    /// </summary>
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public const int MaxPhotosPerWorkOrder = 20;

    public static readonly IReadOnlyDictionary<string, string> AllowedContentTypes = new Dictionary<string, string>
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };
}
