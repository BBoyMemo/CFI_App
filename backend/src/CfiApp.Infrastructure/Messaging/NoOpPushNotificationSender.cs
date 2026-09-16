using CfiApp.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace CfiApp.Infrastructure.Messaging;

/// <summary>
/// Stands in for Firebase Cloud Messaging until the site has FCM credentials (Phase 8/11).
/// Logs the attempt and returns - the caller records the outcome in NotificationLog either
/// way, so nothing downstream needs to know delivery is not real yet.
/// </summary>
public sealed class NoOpPushNotificationSender(ILogger<NoOpPushNotificationSender> logger) : IPushNotificationSender
{
    public Task SendAsync(string deviceToken, string title, string body, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Push notification not sent (no provider configured yet): {Title}", title);
        return Task.CompletedTask;
    }
}
