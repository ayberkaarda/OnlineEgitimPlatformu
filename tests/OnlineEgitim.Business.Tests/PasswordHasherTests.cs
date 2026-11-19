using OnlineEgitim.Business.Security;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Business.Tests;

public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new(iterations: 1_000);

    [Fact]
    public void Hash_VerifiesOnlyTheSamePassword()
    {
        var hash = _hasher.Hash("Gizli.Sifre1");

        Assert.StartsWith("PBKDF2-SHA256$1000$", hash, StringComparison.Ordinal);
        Assert.NotEqual(hash, _hasher.Hash("Gizli.Sifre1")); // salted
        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify("Gizli.Sifre1", hash, PasswordFormat.Pbkdf2));
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify("gizli.sifre1", hash, PasswordFormat.Pbkdf2));
    }

    [Fact]
    public void Verify_FewerIterationsThanCurrent_AsksForARehash()
    {
        var weak = _hasher.Hash("Gizli.Sifre1");

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, new PasswordHasher(2_000).Verify("Gizli.Sifre1", weak, PasswordFormat.Pbkdf2));
    }

    [Theory]
    [InlineData("1234", "1234", PasswordVerificationResult.SuccessRehashNeeded)]
    [InlineData(" 1234 ", "1234", PasswordVerificationResult.SuccessRehashNeeded)]
    [InlineData("12345", "1234", PasswordVerificationResult.Failed)]
    [InlineData("ABCD", "abcd", PasswordVerificationResult.Failed)]
    [InlineData("", "", PasswordVerificationResult.Failed)]
    public void Verify_LegacyPlainText_ComparesTheTrimmedInputOrdinally(string input, string stored, PasswordVerificationResult expected) =>
        Assert.Equal(expected, _hasher.Verify(input, stored, PasswordFormat.LegacyPlainText));

    /// <summary>The stored format decides, never the value: a hash-looking plain text is plain text and vice versa.</summary>
    [Fact]
    public void Verify_NeverReinterpretsTheStoredValue()
    {
        var hash = _hasher.Hash("Gizli.Sifre1");

        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify("Gizli.Sifre1", hash, PasswordFormat.LegacyPlainText));
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, _hasher.Verify(hash, hash, PasswordFormat.LegacyPlainText));
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify("1234", "1234", PasswordFormat.Pbkdf2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("anything")]
    public void Verify_AccountWithoutPassword_NeverSucceeds(string input) =>
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify(input, string.Empty, PasswordFormat.None));

    [Theory]
    [InlineData("PBKDF2-SHA256$abc$x$y")]
    [InlineData("PBKDF2-SHA256$10$AAAA$AAAA")]
    [InlineData("PBKDF2-SHA256$1000$!!$!!")]
    [InlineData("PBKDF2-SHA256$1000$AAAA$AAAA")]
    public void Verify_MalformedHash_Fails(string stored) =>
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify("x", stored, PasswordFormat.Pbkdf2));

    [Fact]
    public void Constructor_RefusesTooFewIterations() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PasswordHasher(999));
}
