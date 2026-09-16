using System.Buffers.Binary;
using System.Security.Cryptography;
using CfiApp.Application.Abstractions;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace CfiApp.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMACSHA256 password hashing.
///
/// The stored value carries its own parameters, so the iteration count can be raised
/// later without invalidating anyone password: an old hash still verifies, and the caller
/// is told to rehash it. Comparison is constant time.
///
/// Format (Base64 of): [version:1][iterations:4][saltLength:4][salt][subkey]
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const byte CurrentVersion = 1;
    private const int SaltSize = 16;
    private const int SubkeySize = 32;

    // OWASP guidance for PBKDF2-HMACSHA256. Raise it, do not lower it.
    private const int CurrentIterations = 600_000;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var subkey = Derive(password, salt, CurrentIterations);

        var payload = new byte[1 + 4 + 4 + salt.Length + subkey.Length];
        payload[0] = CurrentVersion;
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), CurrentIterations);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(5, 4), salt.Length);
        salt.CopyTo(payload.AsSpan(9));
        subkey.CopyTo(payload.AsSpan(9 + salt.Length));

        return Convert.ToBase64String(payload);
    }

    public PasswordVerificationResult Verify(string hash, string password)
    {
        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(password))
        {
            return PasswordVerificationResult.Failed;
        }

        byte[] payload;

        try
        {
            payload = Convert.FromBase64String(hash);
        }
        catch (FormatException)
        {
            // A stored value that is not a hash at all is a failed login, not a crash.
            return PasswordVerificationResult.Failed;
        }

        if (payload.Length < 9 || payload[0] != CurrentVersion)
        {
            return PasswordVerificationResult.Failed;
        }

        var iterations = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(1, 4));
        var saltLength = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(5, 4));

        if (iterations <= 0 || saltLength <= 0 || payload.Length <= 9 + saltLength)
        {
            return PasswordVerificationResult.Failed;
        }

        var salt = payload.AsSpan(9, saltLength).ToArray();
        var expected = payload.AsSpan(9 + saltLength).ToArray();
        var actual = Derive(password, salt, iterations, expected.Length);

        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            return PasswordVerificationResult.Failed;
        }

        return iterations < CurrentIterations
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Success;
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, int size = SubkeySize) =>
        KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, iterations, size);
}
