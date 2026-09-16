using CfiApp.Domain.Common;
using CfiApp.Domain.Media;

namespace CfiApp.Domain.Identity;

/// <summary>
/// Keeps a person logged in across app restarts without asking for the password again.
///
/// Only a hash is stored, tokens rotate on every use, and a token that is presented twice
/// is treated as stolen: the whole chain for that user is revoked. Losing a phone on a
/// factory floor is a realistic event, so this is not optional hardening.
/// </summary>
public sealed class RefreshToken : Entity
{
    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>SHA-256 of the token. The raw value exists only in the client.</summary>
    public required string TokenHash { get; set; }

    /// <summary>The security stamp the token was issued under. A changed stamp invalidates it.</summary>
    public required string SecurityStamp { get; set; }

    public string? DeviceId { get; set; }
    public string? CreatedByIp { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }

    public int? ReplacedByTokenId { get; set; }
    public RefreshToken? ReplacedByToken { get; set; }
}

/// <summary>
/// The fingertip signature a person draws once. It is attached to each work order
/// closure rather than redrawn every time, which is the flow the site asked for.
/// Replacing it keeps the old row: a closure signed last year must still show the
/// signature that was actually used then.
/// </summary>
public sealed class UserSignature : Entity, IAuditable
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public int MediaAssetId { get; set; }
    public MediaAsset? MediaAsset { get; set; }

    public DateTimeOffset? ReplacedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
