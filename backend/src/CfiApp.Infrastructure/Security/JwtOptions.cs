using System.Security.Cryptography;

namespace CfiApp.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "cfi-app";
    public string Audience { get; set; } = "cfi-app";

    /// <summary>
    /// Signing key, base64. Never committed: supplied through user-secrets locally and an
    /// environment variable in production. A random one is generated for Development when
    /// it is missing, so a fresh clone runs without anyone pasting a key into the repo.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Short on purpose. A disabled account keeps working until its access token expires,
    /// so this is the real upper bound on how long a dismissed employee retains access.
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>
    /// Long on purpose: people should not retype a password on a factory floor. Safety
    /// comes from rotation and reuse detection, not from a short lifetime.
    /// </summary>
    public int RefreshTokenDays { get; set; } = 60;

    public byte[] ResolveKeyBytes(bool isDevelopment)
    {
        if (!string.IsNullOrWhiteSpace(Key))
        {
            var bytes = Convert.FromBase64String(Key);

            if (bytes.Length < 32)
            {
                throw new InvalidOperationException(
                    "Jwt:Key must be at least 32 bytes (256 bits) once base64 decoded.");
            }

            return bytes;
        }

        if (!isDevelopment)
        {
            throw new InvalidOperationException(
                "Jwt:Key is not configured. Set it through an environment variable or a secret store.");
        }

        // Development only. Restarting the API invalidates existing tokens, which is fine
        // locally and impossible to mistake for a production configuration.
        return RandomNumberGenerator.GetBytes(64);
    }
}
