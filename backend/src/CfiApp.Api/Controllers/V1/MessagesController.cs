using Asp.Versioning;
using CfiApp.Application.Abstractions;
using CfiApp.Application.Messaging;
using CfiApp.Domain.Identity;
using CfiApp.Infrastructure.Messaging;
using CfiApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

/// <summary>
/// Company messages: sent to a person, several people, or a whole department. The
/// message is the record and stays in the app; a push notification is only the nudge -
/// someone who missed it can still open the inbox and read it.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/messages")]
[Authorize]
public sealed class MessagesController(
    CfiAppDbContext context,
    MessageService service,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Permissions.MessageSend)]
    [ProducesResponseType(typeof(MessageSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MessageSummaryDto>> Send(SendMessageRequest request, CancellationToken cancellationToken)
    {
        var senderId = currentUser.UserId!.Value;
        var message = await service.SendAsync(request, senderId, cancellationToken);

        var senderName = await context.Users
            .Where(x => x.Id == senderId).Select(x => x.FullName).FirstAsync(cancellationToken);

        return CreatedAtAction(nameof(Inbox), new { },
            new MessageSummaryDto(message.Id, message.Body, message.Priority, senderName, message.CreatedAt, false));
    }

    /// <summary>
    /// Everything addressed to the signed-in person - either by name, or because they
    /// were a member of the targeted department at the moment the message went out.
    /// </summary>
    [HttpGet("inbox")]
    [Authorize(Policy = Permissions.MessageRead)]
    [ProducesResponseType(typeof(PagedResult<MessageSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MessageSummaryDto>>> Inbox(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId!.Value;

        var addressedIds = context.MessageRecipients
            .Where(x => x.UserId == userId)
            .Select(x => x.MessageId);

        var fannedOutIds = context.NotificationLogs
            .Where(x => x.UserId == userId && x.MessageId != null && x.Type == "message.new")
            .Select(x => x.MessageId!.Value);

        var messageIds = await addressedIds.Union(fannedOutIds).ToListAsync(cancellationToken);

        var readIds = await context.MessageReads
            .Where(x => x.UserId == userId && messageIds.Contains(x.MessageId))
            .Select(x => x.MessageId)
            .ToListAsync(cancellationToken);

        var query = context.Messages.AsNoTracking()
            .Where(x => messageIds.Contains(x.Id))
            .OrderByDescending(x => x.CreatedAt);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((PagedResult.NormalisePage(page) - 1) * PagedResult.NormalisePageSize(pageSize))
            .Take(PagedResult.NormalisePageSize(pageSize))
            .Select(x => new { x.Id, x.Body, x.Priority, SenderName = x.Sender!.FullName, x.CreatedAt })
            .ToListAsync(cancellationToken);

        var dtos = items
            .Select(x => new MessageSummaryDto(x.Id, x.Body, x.Priority, x.SenderName, x.CreatedAt, readIds.Contains(x.Id)))
            .ToList();

        return Ok(new PagedResult<MessageSummaryDto>(dtos, total, page, pageSize));
    }

    /// <summary>Opening a message marks it read - there is no separate "mark as read" step.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.MessageRead)]
    [ProducesResponseType(typeof(MessageDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MessageDetailDto>> Detail(int id, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId!.Value;

        var message = await context.Messages.AsNoTracking()
            .Include(x => x.Sender)
            .Include(x => x.Recipients).ThenInclude(r => r.User)
            .Include(x => x.Recipients).ThenInclude(r => r.Department)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (message is null) return NotFound();

        var canManage = User.HasClaim(CfiApp.Api.Security.CfiClaimTypes.Permission, Permissions.MessageSend);
        var addressedDirectly = message.Recipients.Any(r => r.UserId == userId);
        var reachedByFanOut = await context.NotificationLogs.AnyAsync(
            x => x.UserId == userId && x.MessageId == id && x.Type == "message.new", cancellationToken);

        if (message.SenderUserId != userId && !addressedDirectly && !reachedByFanOut && !canManage)
        {
            return Problem(title: "This message is not addressed to you", statusCode: StatusCodes.Status403Forbidden);
        }

        if (addressedDirectly || reachedByFanOut)
        {
            await service.MarkReadAsync(id, userId, cancellationToken);
        }

        var labels = message.Recipients
            .Select(r => r.User is not null ? r.User.FullName : $"{r.Department!.Name} (department)")
            .ToList();

        return Ok(new MessageDetailDto(
            message.Id, message.Body, message.Priority, message.Sender!.FullName, message.CreatedAt,
            message.ExpiresAt, labels));
    }

    /// <summary>Who has opened it so far - visible to whoever is allowed to send messages.</summary>
    [HttpGet("{id:int}/read-receipts")]
    [Authorize(Policy = Permissions.MessageSend)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ReadReceiptDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ReadReceiptDto>>> ReadReceipts(
        int id, CancellationToken cancellationToken)
    {
        var reads = await context.MessageReads.AsNoTracking()
            .Where(x => x.MessageId == id)
            .Select(x => new ReadReceiptDto(x.UserId, x.User!.FullName, x.ReadAt))
            .ToListAsync(cancellationToken);

        return Ok(reads);
    }
}

/// <summary>Where a device registers itself so a push can reach it, and where it un-registers on sign out.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/me/device-tokens")]
[Authorize]
public sealed class DeviceTokensController(CfiAppDbContext context, IClock clock, ICurrentUser currentUser) : ControllerBase
{
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Register(RegisterDeviceTokenRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId!.Value;
        var now = clock.UtcNow;

        var existing = await context.DeviceTokens.FirstOrDefaultAsync(x => x.Token == request.Token, cancellationToken);

        if (existing is null)
        {
            context.DeviceTokens.Add(new Domain.Messaging.DeviceToken
            {
                UserId = userId,
                Token = request.Token,
                Platform = request.Platform,
                LastSeenAt = now
            });
        }
        else
        {
            // The same device signing in as a different account (a shared tablet, a
            // reinstall) hands the token to whoever is signed in now.
            existing.UserId = userId;
            existing.Platform = request.Platform;
            existing.LastSeenAt = now;
            existing.RevokedAt = null;
        }

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke([FromBody] string token, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId!.Value;

        var deviceToken = await context.DeviceTokens
            .FirstOrDefaultAsync(x => x.Token == token && x.UserId == userId, cancellationToken);

        if (deviceToken is not null)
        {
            deviceToken.RevokedAt = clock.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }
}
