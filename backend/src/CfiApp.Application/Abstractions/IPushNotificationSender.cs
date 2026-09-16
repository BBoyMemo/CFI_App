namespace CfiApp.Application.Abstractions;

/// <summary>
/// Sends one push notification to one device. The Phase 8 boundary: everything above this
/// interface - who gets notified, what the message says, that it was attempted - is real
/// and tested now. Only the delivery mechanism is a stand-in until Firebase credentials
/// exist, at which point only the implementation behind this interface changes.
/// </summary>
public interface IPushNotificationSender
{
    Task SendAsync(string deviceToken, string title, string body, CancellationToken cancellationToken);
}
