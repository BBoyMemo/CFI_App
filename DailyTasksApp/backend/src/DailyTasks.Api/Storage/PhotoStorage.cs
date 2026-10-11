namespace DailyTasks.Api.Storage;

public class StorageOptions
{
    public const string Section = "Storage";

    // Relative paths resolve against the app's content root.
    public string RootPath { get; set; } = "storage";
    public long MaxPhotoBytes { get; set; } = 10 * 1024 * 1024;
}

// Photos live behind this interface so local disk can later be swapped for cloud storage.
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, CancellationToken ct);
    Stream? OpenRead(string key);
    void Delete(string key);
}

public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(Microsoft.Extensions.Options.IOptions<StorageOptions> options, IWebHostEnvironment env)
    {
        _root = Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.RootPath));
        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(string key, Stream content, CancellationToken ct)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public Stream? OpenRead(string key)
    {
        var path = Resolve(key);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public void Delete(string key)
    {
        var path = Resolve(key);
        if (File.Exists(path)) File.Delete(path);
    }

    private string Resolve(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Storage key escapes the storage root.");
        return path;
    }
}

public record PhotoFormat(string Extension, string ContentType);

public class PhotoService(IFileStorage storage, Microsoft.Extensions.Options.IOptions<StorageOptions> options)
{
    public long MaxBytes => options.Value.MaxPhotoBytes;

    // Returns an error code for the client, or null when the photo is acceptable.
    public async Task<(string? Key, string? Error)> SaveAsync(IFormFile photo, string folder, CancellationToken ct)
    {
        if (photo.Length == 0) return (null, "photo.empty");
        if (photo.Length > MaxBytes) return (null, "photo.tooLarge");

        await using var stream = photo.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        var format = Detect(header.AsSpan(0, read));
        if (format is null) return (null, "photo.unsupportedFormat");

        stream.Position = 0;
        var key = $"{folder}/{Guid.NewGuid():N}{format.Extension}";
        await storage.SaveAsync(key, stream, ct);
        return (key, null);
    }

    public (Stream Content, string ContentType)? Open(string key)
    {
        var stream = storage.OpenRead(key);
        if (stream is null) return null;
        var contentType = Path.GetExtension(key) switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg",
        };
        return (stream, contentType);
    }

    public void Delete(string? key)
    {
        if (key is not null) storage.Delete(key);
    }

    // Trust the file's signature bytes, not the client-supplied name or content type.
    private static PhotoFormat? Detect(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF)
            return new(".jpg", "image/jpeg");
        if (h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return new(".png", "image/png");
        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8))
            return new(".webp", "image/webp");
        return null;
    }
}
