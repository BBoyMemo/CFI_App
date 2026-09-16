using CfiApp.Domain.Common;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Organization;

namespace CfiApp.Domain.Messaging;

public enum MessagePriority
{
    Normal = 0,
    High = 1
}

/// <summary>
/// A company message. The message is the content and it stays in the app; the push
/// notification is only the nudge. Someone who missed the notification must still be able
/// to open the app and read what was sent.
/// </summary>
public sealed class Message : Entity, IAuditable
{
    public int SenderUserId { get; set; }
    public User? Sender { get; set; }

    public required string Body { get; set; }
    public MessagePriority Priority { get; set; } = MessagePriority.Normal;

    public DateTimeOffset? ExpiresAt { get; set; }

    public ICollection<MessageRecipient> Recipients { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

/// <summary>
/// Who a message was addressed to: either one person or a whole department.
/// Exactly one of the two is set, and the database enforces that.
/// </summary>
public sealed class MessageRecipient : Entity
{
    public int MessageId { get; set; }
    public Message? Message { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
}

public sealed class MessageRead
{
    public int MessageId { get; set; }
    public Message? Message { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public DateTimeOffset ReadAt { get; set; }
}

public enum DevicePlatform
{
    Android = 0,
    IOS = 1,
    Web = 2
}

/// <summary>Push target for one device. Revoked when the person signs out or the token dies.</summary>
public sealed class DeviceToken : Entity, IAuditable
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public required string Token { get; set; }
    public DevicePlatform Platform { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

public enum NotificationDelivery
{
    Queued = 0,
    Sent = 1,
    Failed = 2
}

/// <summary>
/// What was actually pushed, to whom, and whether it arrived. Also covers system
/// notifications such as "engineer on the way" and "waiting for parts", so there is one
/// place to look when someone says they were never told.
/// </summary>
public sealed class NotificationLog : Entity, IAppendOnly
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public int? MessageId { get; set; }
    public Message? Message { get; set; }

    /// <summary>Short machine readable kind, e.g. workorder.onMyWay or holiday.approved.</summary>
    public required string Type { get; set; }

    public DateTimeOffset SentAt { get; set; }
    public NotificationDelivery Delivery { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
