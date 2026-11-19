using Microsoft.Extensions.Time.Testing;
using Moq;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Tests;

public sealed class AuthServiceTests
{
    private static readonly DateTime Now = new(2026, 3, 2, 10, 10, 0, DateTimeKind.Utc);

    private readonly Mock<IAccountRepository> _accounts = new(MockBehavior.Strict);
    private readonly PasswordHasher _hasher = new(iterations: 1_000);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));
    private readonly RecordingAudit _audit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AuthService Service => new(_accounts.Object, _hasher, _time, _audit);

    [Fact]
    public async Task SignIn_RightPassword_IssuesASessionOfTheRequestedRole()
    {
        var account = Account(_hasher.Hash("Gizli.Sifre1"), PasswordFormat.Pbkdf2);
        Find(AccountRole.Teacher, account);
        _accounts.Setup(a => a.RecordSuccessfulLoginAsync(AccountRole.Teacher, account, Now, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var session = await Service.SignInTeacherAsync(" Deniz Örnek ", "Gizli.Sifre1", Ct);

        Assert.Equal(AccountRole.Teacher, session.Role);
        Assert.Equal(7, session.UserId);
        Assert.Equal([(AuditEvent.SignInSucceeded, (AccountRole?)AccountRole.Teacher)], _audit.Events);
    }

    /// <summary>
    /// Every refused sign-in gets one message and one audit code, so neither the user nor whoever reads
    /// the log learns whether the name exists, is locked, has no password or lost a race.
    /// </summary>
    [Fact]
    public async Task SignIn_EveryKindOfRefusal_SameMessageAndSameAuditCode()
    {
        var good = _hasher.Hash("Gizli.Sifre1");
        var messages = new List<string>();

        // Unknown name.
        _accounts.Setup(a => a.FindAsync(AccountRole.Student, "Bilinmeyen", It.IsAny<CancellationToken>())).ReturnsAsync((AccountCredentials?)null);
        messages.Add((await Assert.ThrowsAsync<BusinessRuleException>(() => Service.SignInStudentAsync("Bilinmeyen", "Gizli.Sifre1", Ct))).Message);

        // Wrong password.
        var wrong = Account(good, PasswordFormat.Pbkdf2, fullName: "Yanlis");
        Find(AccountRole.Student, wrong);
        _accounts.Setup(a => a.RecordFailedLoginAsync(AccountRole.Student, 7, LoginPolicy.MaxFailedAttempts, Now + LoginPolicy.LockoutDuration, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        messages.Add((await Assert.ThrowsAsync<BusinessRuleException>(() => Service.SignInStudentAsync("Yanlis", "baska", Ct))).Message);

        // Locked account with the right password.
        Find(AccountRole.Student, Account(good, PasswordFormat.Pbkdf2, fullName: "Kilitli", lockoutEnd: Now.AddMinutes(1)));
        messages.Add((await Assert.ThrowsAsync<BusinessRuleException>(() => Service.SignInStudentAsync("Kilitli", "Gizli.Sifre1", Ct))).Message);

        // Account without a password.
        Find(AccountRole.Student, Account(string.Empty, PasswordFormat.None, fullName: "Sifresiz"));
        messages.Add((await Assert.ThrowsAsync<BusinessRuleException>(() => Service.SignInStudentAsync("Sifresiz", "x", Ct))).Message);

        // Right password, but a concurrent failure locked the account before the success was written.
        var raced = Account(good, PasswordFormat.Pbkdf2, fullName: "Yaris");
        Find(AccountRole.Student, raced);
        _accounts.Setup(a => a.RecordSuccessfulLoginAsync(AccountRole.Student, raced, Now, null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        messages.Add((await Assert.ThrowsAsync<BusinessRuleException>(() => Service.SignInStudentAsync("Yaris", "Gizli.Sifre1", Ct))).Message);

        Assert.All(messages, m => Assert.Equal(AuthService.CredentialsInvalid, m));
        Assert.Equal(5, _audit.Events.Count);
        Assert.All(_audit.Events, e => Assert.Equal((AuditEvent.SignInFailed, (AccountRole?)AccountRole.Student), e));
    }

    [Fact]
    public async Task SignIn_LockedAccount_WritesNothing()
    {
        Find(AccountRole.Teacher, Account(_hasher.Hash("Gizli.Sifre1"), PasswordFormat.Pbkdf2, lockoutEnd: Now.AddMinutes(1)));

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service.SignInTeacherAsync("Deniz Örnek", "yanlis", Ct));

        _accounts.Verify(a => a.RecordFailedLoginAsync(It.IsAny<AccountRole>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SignIn_LegacyPlainText_IsReplacedByAHashInTheSuccessWrite()
    {
        var account = Account("1234", PasswordFormat.LegacyPlainText);
        Find(AccountRole.Teacher, account);
        string? written = null;
        _accounts
            .Setup(a => a.RecordSuccessfulLoginAsync(AccountRole.Teacher, account, Now, It.IsNotNull<string>(), It.IsAny<CancellationToken>()))
            .Callback<AccountRole, AccountCredentials, DateTime, string?, CancellationToken>((_, _, _, hash, _) => written = hash)
            .ReturnsAsync(true);

        await Service.SignInTeacherAsync("Deniz Örnek", " 1234 ", Ct);

        Assert.NotNull(written);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify("1234", written, PasswordFormat.Pbkdf2));
        Assert.Contains((AuditEvent.PasswordUpgraded, (AccountRole?)AccountRole.Teacher), _audit.Events);
    }

    [Theory]
    [InlineData(null, "x")]
    [InlineData("  ", "x")]
    [InlineData("Deniz Örnek", null)]
    [InlineData("Deniz Örnek", "")]
    public async Task SignIn_MissingInput_AsksForItWithoutTouchingTheAccounts(string? name, string? password)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.SignInStudentAsync(name, password, Ct));

        Assert.Equal(AuthService.InputRequired, error.Message);
        _accounts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SignIn_LegacyLengthPassword_IsAcceptedUpToTheOldColumnSize()
    {
        var longest = new string('x', PasswordPolicy.SignInMaxLength);
        Find(AccountRole.Student, Account(longest, PasswordFormat.LegacyPlainText));
        _accounts.Setup(a => a.RecordSuccessfulLoginAsync(AccountRole.Student, It.IsAny<AccountCredentials>(), Now, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Service.SignInStudentAsync("Deniz Örnek", longest, Ct);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service.SignInStudentAsync("Deniz Örnek", longest + "x", Ct));
    }

    [Theory]
    [InlineData(null, "a@example.com", "Gizli.Sifre1", "Lütfen tüm alanları doldurunuz.")]
    [InlineData("Ayşe", "  ", "Gizli.Sifre1", "Lütfen tüm alanları doldurunuz.")]
    [InlineData("Ayşe", "a@example.com", " ", "Lütfen tüm alanları doldurunuz.")]
    [InlineData("Ayşe  Yılmaz", "a@example.com", "Gizli.Sifre1", null)]
    [InlineData("Ayşe1", "a@example.com", "Gizli.Sifre1", null)]
    [InlineData("Ayşe", "a-example.com", "Gizli.Sifre1", "Lütfen geçerli bir e-posta adresi girin.")]
    [InlineData("Ayşe", "a@example.com", "kisa", null)]
    public async Task RegisterStudent_InvalidInput_IsRefusedBeforeAnyWrite(string? name, string? email, string? password, string? message)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.RegisterStudentAsync(name, email, password, Ct));

        if (message is not null)
        {
            Assert.Equal(message, error.Message);
        }

        _accounts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RegisterStudent_ConcurrentDuplicate_GetsTheSameAnswerAsAKnownOne()
    {
        _accounts.Setup(a => a.StudentExistsAsync("Ayşe Yılmaz", "ayse@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _accounts.Setup(a => a.AddStudentAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>())).ThrowsAsync(new DuplicateAccountException());

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.RegisterStudentAsync("Ayşe Yılmaz", "ayse@example.com", "Gizli.Sifre1", Ct));

        Assert.Equal(AuthService.AccountTaken, error.Message);
        Assert.Empty(_audit.Events);
    }

    [Fact]
    public async Task RegisterStudent_StoresATrimmedNameAndAHash()
    {
        Student? stored = null;
        _accounts.Setup(a => a.StudentExistsAsync("Ayşe Yılmaz", "ayse@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _accounts.Setup(a => a.AddStudentAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()))
            .Callback<Student, CancellationToken>((s, _) => stored = s)
            .ReturnsAsync(3);

        await Service.RegisterStudentAsync(" Ayşe Yılmaz ", " ayse@example.com ", "Gizli.Sifre1", Ct);

        Assert.NotNull(stored);
        Assert.Equal("Ayşe Yılmaz", stored.FullName);
        Assert.Equal(PasswordFormat.Pbkdf2, stored.PasswordFormat);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify("Gizli.Sifre1", stored.PasswordHash, PasswordFormat.Pbkdf2));
        Assert.Equal([(AuditEvent.StudentRegistered, (AccountRole?)AccountRole.Student)], _audit.Events);
    }

    private void Find(AccountRole role, AccountCredentials account) =>
        _accounts.Setup(a => a.FindAsync(role, account.FullName, It.IsAny<CancellationToken>())).ReturnsAsync(account);

    private static AccountCredentials Account(string hash, PasswordFormat format, string fullName = "Deniz Örnek", DateTime? lockoutEnd = null) =>
        new(7, fullName, hash, format, 0, lockoutEnd);
}
