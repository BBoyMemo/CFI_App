using CfiApp.Domain.Common;

namespace CfiApp.Domain.Media;

/// <summary>
/// One row per stored file: breakdown photos, closing photos, equipment pictures,
/// signatures. The bytes live behind IFileStorage - local disk today, object storage
/// later - so moving the files never changes this table.
/// </summary>
public sealed class MediaAsset : Entity, IAuditable
{
    /// <summary>Opaque key inside the storage provider. Never the original file name.</summary>
    public required string StorageKey { get; set; }

    public required string ContentType { get; set; }
    public long ByteSize { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }

    /// <summary>Content hash, so the same photo uploaded twice can be recognised.</summary>
    public required string Sha256 { get; set; }

    /// <summary>The name the file arrived with, kept for the audit trail only.</summary>
    public string? OriginalFileName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
