using CfiApp.Application.Abstractions;
using CfiApp.Application.Messaging;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Messaging;
using CfiApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Infrastructure.Messaging;

/// <summary>
/// Sending a message. The message itself is the content and stays in the app; the push
/// is only the nudge - which is why who was actually notified is resolved and written
/// down (as NotificationLog rows) at the moment of sending, not recomputed later. Someone
/// who joins a department after a message went out never sees it appear retroactively.
/// </summary>
public sealed class MessageService(
    CfiAppDbContext context,
    IPushNotificationSender pushSender,
    IClock clock)
{
    public async Task<Message> SendAsync(SendMessageRequest request, int senderUserId, CancellationToken cancellationToken)
    {
        var userIds = request.RecipientUserIds.Distinct().ToArray();
        var departmentIds = request.RecipientDepartmentIds.Distinct().ToArray();

        var knownUserCount = await context.Users.CountAsync(x => userIds.Contains(x.Id), cancellationToken);
        if (knownUserCount != userIds.Length)
        {
            throw new UnknownMessageTargetException("One or more recipients are unknown.");
        }

        var knownDepartmentCount = await context.Departments.CountAsync(x => departmentIds.Contains(x.Id), cancellationToken);
        if (knownDepartmentCount != departmentIds.Length)
        {
            throw new UnknownMessageTargetException("One or more departments are unknown.");
        }

        var now = clock.UtcNow;

        var message = new Message
        {
            SenderUserId = senderUserId,
            Body = request.Body.Trim(),
            Priority = request.Priority,
            ExpiresAt = request.ExpiresAt
        };

        foreach (var userId in userIds)
        {
            message.Recipients.Add(new MessageRecipient { UserId = userId });
        }

        foreach (var departmentId in departmentIds)
        {
            message.Recipients.Add(new MessageRecipient { DepartmentId = departmentId });
        }

        context.Messages.Add(message);
        await context.SaveChangesAsync(cancellationToken);

        // Department membership is resolved once, right now - not every time the inbox is
        // read - so the message never retroactively appears for someone who joins later.
        var departmentUserIds = departmentIds.Length == 0
            ? []
            : await context.Users
                .Where(x => x.DepartmentId != null && departmentIds.Contains(x.DepartmentId.Value)
                            && x.Status == UserStatus.Active)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

        var allRecipientUserIds = userIds.Concat(departmentUserIds).Distinct().ToArray();

        foreach (var userId in allRecipientUserIds)
        {
            context.NotificationLogs.Add(new NotificationLog
            {
                UserId = userId,
                MessageId = message.Id,
                Type = "message.new",
                SentAt = now,
                Delivery = NotificationDelivery.Queued
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        await PushToDevicesAsync(allRecipientUserIds, message, cancellationToken);

        return message;
    }

    private async Task PushToDevicesAsync(int[] userIds, Message message, CancellationToken cancellationToken)
    {
        if (userIds.Length == 0) return;

        var tokens = await context.DeviceTokens
            .Where(x => userIds.Contains(x.UserId) && x.RevokedAt == null)
            .Select(x => x.Token)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            await pushSender.SendAsync(token, "New message", message.Body, cancellationToken);
        }
    }

    public async Task MarkReadAsync(int messageId, int userId, CancellationToken cancellationToken)
    {
        var alreadyRead = await context.MessageReads
            .AnyAsync(x => x.MessageId == messageId && x.UserId == userId, cancellationToken);

        if (alreadyRead) return;

        context.MessageReads.Add(new MessageRead { MessageId = messageId, UserId = userId, ReadAt = clock.UtcNow });
        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class UnknownMessageTargetException(string reason) : Exception(reason);
