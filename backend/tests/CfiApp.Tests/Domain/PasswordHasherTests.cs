using CfiApp.Application.Abstractions;
using CfiApp.Infrastructure.Security;
using Shouldly;

namespace CfiApp.Tests.Domain;

public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void A_correct_password_verifies()
    {
        var hash = _hasher.Hash("Winter-Shift-2026!");

        _hasher.Verify(hash, "Winter-Shift-2026!").ShouldBe(PasswordVerificationResult.Success);
    }

    [Fact]
    public void A_wrong_password_does_not_verify()
    {
        var hash = _hasher.Hash("Winter-Shift-2026!");

        _hasher.Verify(hash, "winter-shift-2026!").ShouldBe(PasswordVerificationResult.Failed);
        _hasher.Verify(hash, "something else").ShouldBe(PasswordVerificationResult.Failed);
    }

    [Fact]
    public void The_same_password_never_produces_the_same_hash()
    {
        var first = _hasher.Hash("Winter-Shift-2026!");
        var second = _hasher.Hash("Winter-Shift-2026!");

        first.ShouldNotBe(second, "each hash carries its own random salt");
    }

    [Fact]
    public void The_password_is_not_recoverable_from_the_hash()
    {
        var hash = _hasher.Hash("Winter-Shift-2026!");

        hash.ShouldNotContain("Winter");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-base64-at-all!!")]
    [InlineData("AAAA")]
    public void A_damaged_or_missing_stored_hash_is_a_failed_login_not_a_crash(string stored)
    {
        _hasher.Verify(stored, "Winter-Shift-2026!").ShouldBe(PasswordVerificationResult.Failed);
    }

    [Fact]
    public void An_empty_password_is_rejected_at_hash_time()
    {
        Should.Throw<ArgumentException>(() => _hasher.Hash(""));
        Should.Throw<ArgumentException>(() => _hasher.Hash("   "));
    }

    [Fact]
    public void Verifying_an_empty_password_fails_rather_than_throwing()
    {
        var hash = _hasher.Hash("Winter-Shift-2026!");

        _hasher.Verify(hash, "").ShouldBe(PasswordVerificationResult.Failed);
    }
}
