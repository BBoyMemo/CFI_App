namespace CfiApp.Application.Abstractions;

public enum PasswordVerificationResult
{
    Failed = 0,
    Success = 1,
    /// <summary>
    /// Correct password, but stored with older parameters. The caller rehashes it on the
    /// spot, so the whole user base drifts to stronger settings without a migration.
    /// </summary>
    SuccessRehashNeeded = 2
}

public interface IPasswordHasher
{
    string Hash(string password);
    PasswordVerificationResult Verify(string hash, string password);
}
