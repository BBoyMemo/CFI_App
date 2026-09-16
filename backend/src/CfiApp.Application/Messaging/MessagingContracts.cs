using CfiApp.Domain.Messaging;

namespace CfiApp.Application.Messaging;

public sealed record SendMessageRequest(
    string Body,
    MessagePriority Priority,
    DateTimeOffset? ExpiresAt,
    IReadOnlyCollection<int> RecipientUserIds,
    IReadOnlyCollection<int> RecipientDepartmentIds);

public sealed record MessageSummaryDto(
    int Id, string Body, MessagePriority Priority, string SenderName, DateTimeOffset CreatedAt, bool IsRead);

public sealed record MessageDetailDto(
    int Id, string Body, MessagePriority Priority, string SenderName, DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt, IReadOnlyCollection<string> RecipientLabels);

public sealed record ReadReceiptDto(int UserId, string FullName, DateTimeOffset? ReadAt);

public sealed record RegisterDeviceTokenRequest(string Token, DevicePlatform Platform);

public sealed record NotificationDto(
    int Id, string Type, DateTimeOffset SentAt, int? MessageId, string? MessageBody,
    string? SenderName);

public sealed record UnseenNotificationsDto(int Count);
