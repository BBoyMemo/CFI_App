using Asp.Versioning;
using CfiApp.Application.Abstractions;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Media;
using CfiApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

/// <summary>
/// The one-time fingertip signature. Registered once, attached to every closure a person
/// signs afterwards - they are not asked to draw it again each time. Replacing it keeps
/// the old row (ReplacedAt is set rather than the row being deleted), so a closure signed
/// last year still shows the signature that was actually used then.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/me/signature")]
[Authorize]
public sealed class SignatureController(
    CfiAppDbContext context,
    IFileStorage fileStorage,
    IClock clock,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPut]
    [RequestSizeLimit(UploadPolicy.MaxFileSizeBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Problem(title: "The file is empty", statusCode: StatusCodes.Status400BadRequest);
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

        var asset = new MediaAsset
        {
            StorageKey = stored.StorageKey,
            ContentType = file.ContentType,
            ByteSize = stored.ByteSize,
            Sha256 = stored.Sha256Hex
        };
        context.MediaAssets.Add(asset);

        var userId = currentUser.UserId!.Value;

        var previous = await context.UserSignatures
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ReplacedAt == null, cancellationToken);

        if (previous is not null)
        {
            previous.ReplacedAt = clock.UtcNow;
        }

        context.UserSignatures.Add(new UserSignature { UserId = userId, MediaAsset = asset });

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> HasSignature(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId!.Value;

        var exists = await context.UserSignatures
            .AnyAsync(x => x.UserId == userId && x.ReplacedAt == null, cancellationToken);

        return Ok(exists);
    }
}
