using Asp.Versioning;
using CfiApp.Application.Abstractions;
using CfiApp.Application.Messaging;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Messaging;
using CfiApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

/// <summary>
/// What this person needs telling about: messages a manager has sent them, and the few
/// system events that are about them rather than about a job.
///
/// The notification log holds more than that - an engineer saying they are on their way, a
/// job held up waiting for a part, a closure QA sent back - and those deliberately stay off
/// this screen. A job already has a card showing exactly where it has got to, and repeating
/// it here turned the screen into a list nobody read.
///
/// A shift change is the other kind of thing. It is not progress on a job somebody can go
/// and look at; it is a change to the person's own week that they would otherwise discover
/// by turning up at the wrong time. So the filter is a named list rather than "anything
/// with a message attached" - each type is on this screen because somebody decided it
/// should be.
///
/// The log itself keeps every row either way: this is a filter over what is shown, not a
/// decision to stop recording.
///
/// Append-only, like the log: there is nothing to mark, delete or tidy here.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize]
public sealed class NotificationsController(CfiAppDbContext context, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<NotificationDto>>> Mine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId!.Value;

        var query = FeedFor(userId).OrderByDescending(x => x.SentAt);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((PagedResult.NormalisePage(page) - 1) * PagedResult.NormalisePageSize(pageSize))
            .Take(PagedResult.NormalisePageSize(pageSize))
            .Select(x => new NotificationDto(
                x.Id,
                x.Type,
                x.SentAt,
                x.MessageId,
                // Carried through so a row can show what the message said and who sent it,
                // instead of making somebody open a second screen to find out.
                x.Message != null ? x.Message.Body : null,
                x.Message != null && x.Message.Sender != null ? x.Message.Sender.FullName : null))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<NotificationDto>(items, total, page, pageSize));
    }

    /// <summary>
    /// How many have arrived since the person last looked, for the dot on the menu icon.
    /// The client passes the timestamp it last displayed; the server does not track a
    /// read state, because a notification is not something you tick off.
    /// </summary>
    [HttpGet("unseen-count")]
    [ProducesResponseType(typeof(UnseenNotificationsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UnseenNotificationsDto>> UnseenCount(
        [FromQuery] DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId!.Value;

        // Counted over the same rows the screen lists. A badge promising three things
        // that are not there when you tap it is worse than no badge.
        var query = FeedFor(userId);
        if (since is not null) query = query.Where(x => x.SentAt > since);

        return Ok(new UnseenNotificationsDto(await query.CountAsync(cancellationToken)));
    }

    /// <summary>
    /// System notification types that belong on this screen. Work order progress is not
    /// here on purpose - see the note on the class.
    /// </summary>
    private static readonly string[] FeedTypes =
        ["shift.rosterChanged", "shift.coverAdded", "shift.coverRemoved"];

    /// <summary>
    /// This person's rows worth showing: anything that came from a message, plus the named
    /// system types above.
    /// </summary>
    private IQueryable<NotificationLog> FeedFor(int userId) =>
        context.NotificationLogs
            .AsNoTracking()
            .Where(x => x.UserId == userId && (x.MessageId != null || FeedTypes.Contains(x.Type)));
}
