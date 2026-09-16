using Asp.Versioning;
using CfiApp.Application.Abstractions;
using CfiApp.Application.Media;
using CfiApp.Domain.Media;
using CfiApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

/// <summary>
/// Upload a photo, get back an id. A breakdown report or a closure form references
/// photos by id rather than embedding them directly - the client uploads as pictures are
/// taken, then submits the form with the ids it already has.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/media")]
[Authorize]
public sealed class MediaController(CfiAppDbContext context, IFileStorage fileStorage) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(UploadPolicy.MaxFileSizeBytes)]
    [ProducesResponseType(typeof(MediaAssetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MediaAssetDto>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Problem(title: "The file is empty", statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length > UploadPolicy.MaxFileSizeBytes)
        {
            return Problem(
                title: "File too large",
                detail: $"Photos must be {UploadPolicy.MaxFileSizeBytes / (1024 * 1024)} MB or smaller.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!UploadPolicy.AllowedContentTypes.TryGetValue(file.ContentType, out var extension))
        {
            return Problem(
                title: "Unsupported file type",
                detail: $"Allowed types: {string.Join(", ", UploadPolicy.AllowedContentTypes.Keys)}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        await using var stream = file.OpenReadStream();
        var stored = await fileStorage.SaveAsync(stream, extension, cancellationToken);

        // The same photo saved twice (a retried upload, a duplicate tap) reuses the row -
        // the storage key is content-addressed, so this is a lookup, not a new file.
        var existing = await context.MediaAssets
            .FirstOrDefaultAsync(x => x.StorageKey == stored.StorageKey, cancellationToken);

        if (existing is not null)
        {
            return CreatedAtAction(nameof(Upload), new { },
                new MediaAssetDto(existing.Id, existing.ContentType, existing.ByteSize, existing.CreatedAt));
        }

        var asset = new MediaAsset
        {
            StorageKey = stored.StorageKey,
            ContentType = file.ContentType,
            ByteSize = stored.ByteSize,
            Sha256 = stored.Sha256Hex,
            OriginalFileName = Path.GetFileName(file.FileName)
        };

        context.MediaAssets.Add(asset);
        await context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Upload), new { },
            new MediaAssetDto(asset.Id, asset.ContentType, asset.ByteSize, asset.CreatedAt));
    }

    /// <summary>
    /// Serves the file back. Any signed in user may fetch any media by id: the work
    /// order (or task, or signature) it belongs to is already permission-gated, and a
    /// bare numeric id is not sensitive enough on its own to warrant re-deriving that
    /// same ownership check here.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var asset = await context.MediaAssets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (asset is null) return NotFound();

        var stream = await fileStorage.OpenReadAsync(asset.StorageKey, cancellationToken);
        if (stream is null) return NotFound();

        Response.Headers.CacheControl = "private, max-age=86400";
        return File(stream, asset.ContentType);
    }
}
