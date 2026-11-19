using Microsoft.Extensions.Time.Testing;
using Moq;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Tests;

/// <summary>Changing one's own password and administrators' reset codes, with strict mocks.</summary>
public sealed class PasswordResetTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAccountRepository> _accounts = new(MockBehavior.Strict);
    private readonly Mock<IAccountAdministrationRepository> _administration = new(MockBehavior.Strict);
    private readonly PasswordHasher _hasher = new(iterations: 1_000);
    private readonly RecordingAudit _audit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AuthService Auth => new(_accounts.Object, _hasher, new FakeTimeProvider(Now), _audit, Validators.Registration, Validators.SignIn, CurrentSessions.Instance, Validators.ChangePassword);

    private AccountCredentials Stored(bool disabled = false, DateTime? lockoutEnd = null) =>
        new(5, "Mert Deneme", _hasher.Hash("Eski.Sifre1"), PasswordFormat.Pbkdf2, 0, lockoutEnd, AccountRole.Student, disabled, 2);

    [Fact]
    public async Task Change_RightCurrentPassword_WritesTheNewHashForTheVerifiedState()
    {
        var stored = Stored();
        string? written = null;
        _accounts.Setup(a => a.FindByIdAsync(AccountRole.Student, 5, It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        _accounts.Setup(a => a.ChangePasswordAsync(AccountRole.Student, stored, Now.UtcDateTime, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<AccountRole, AccountCredentials, DateTime, string, CancellationToken>((_, _, _, hash, _) => written = hash)
            .ReturnsAsync(true);

        await Auth.ChangePasswordAsync(Sessions.Student(5), "Eski.Sifre1", "Yeni.Sifre1", "Yeni.Sifre1", Ct);

        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify("Yeni.Sifre1", written!, PasswordFormat.Pbkdf2));
        Assert.Equal([(AuditEvent.PasswordChanged, (AccountRole?)AccountRole.Student)], _audit.Events);
    }

    /// <summary>A wrong current password counts towards the lockout; a locked or disabled account and a lost race get the same answer and write nothing.</summary>
    [Fact]
    public async Task Change_EveryRefusal_OneMessageOneAuditCode()
    {
        _accounts.Setup(a => a.RecordFailedLoginAsync(AccountRole.Student, 5, LoginPolicy.MaxFailedAttempts, Now.UtcDateTime + LoginPolicy.LockoutDuration, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var messages = new List<string>();

        foreach (var state in new[] { Stored(), Stored(lockoutEnd: Now.UtcDateTime.AddMinutes(1)), Stored(disabled: true) })
        {
            _accounts.Setup(a => a.FindByIdAsync(AccountRole.Student, 5, It.IsAny<CancellationToken>())).ReturnsAsync(state);
            var current = state.Disabled || state.LockoutEnd is not null ? "Eski.Sifre1" : "Yanlis.Sifre1";
            messages.Add((await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.ChangePasswordAsync(Sessions.Student(5), current, "Yeni.Sifre1", "Yeni.Sifre1", Ct))).Message);
        }

        var raced = Stored();
        _accounts.Setup(a => a.FindByIdAsync(AccountRole.Student, 5, It.IsAny<CancellationToken>())).ReturnsAsync(raced);
        _accounts.Setup(a => a.ChangePasswordAsync(AccountRole.Student, raced, Now.UtcDateTime, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        messages.Add((await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.ChangePasswordAsync(Sessions.Student(5), "Eski.Sifre1", "Yeni.Sifre1", "Yeni.Sifre1", Ct))).Message);

        Assert.All(messages, m => Assert.Equal(AuthService.PasswordChangeFailed, m));
        Assert.Equal(4, _audit.Events.Count);
        Assert.All(_audit.Events, e => Assert.Equal((AuditEvent.PasswordChangeFailed, (AccountRole?)AccountRole.Student), e));
        _accounts.Verify(a => a.RecordFailedLoginAsync(It.IsAny<AccountRole>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("", "Yeni.Sifre1", "Yeni.Sifre1", "Lütfen tüm alanları doldurun.")]
    [InlineData("Eski.Sifre1", "Yeni.Sifre1", "Yeni.Sifre2", "Şifreler uyuşmuyor.")]
    [InlineData("Eski.Sifre1", "kisa", "kisa", "Şifre en az 8, en fazla 64 karakter olmalıdır.")]
    public async Task Change_InvalidInput_ReachesNoRepository(string current, string password, string repeat, string message)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.ChangePasswordAsync(Sessions.Student(5), current, password, repeat, Ct));

        Assert.Equal(message, error.Message);
        _accounts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResetCode_StoresTheHashForTheGivenAccountOnly()
    {
        string? hash = null;
        _administration.Setup(a => a.SetRecoveryCodeAsync(AccountRole.Teacher, 4, It.IsAny<string>(), Now.UtcDateTime + RecoveryPolicy.CodeLifetime, It.IsAny<CancellationToken>()))
            .Callback<AccountRole, int, string, DateTime, CancellationToken>((_, _, h, _, _) => hash = h)
            .ReturnsAsync(AccountWriteStatus.Done);
        _administration.Setup(a => a.SetRecoveryCodeAsync(AccountRole.Admin, 4, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountWriteStatus.NotFound);
        var service = new AdministrationService(_accounts.Object, _administration.Object, _hasher, new FakeTimeProvider(Now), _audit, CurrentSessions.Instance, new TeacherAccountInputValidator(), new StudentAccountInputValidator(), new AccountSearchValidator());

        var code = await service.IssueResetCodeAsync(Sessions.Admin(), AccountRole.Teacher, 4, Ct);
        var wrongRole = await Assert.ThrowsAsync<BusinessRuleException>(() => service.IssueResetCodeAsync(Sessions.Admin(), AccountRole.Admin, 4, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.IssueResetCodeAsync(Sessions.Teacher(), AccountRole.Student, 1, Ct));

        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify(CodeFormat.Normalize(code.Code), hash!, PasswordFormat.Pbkdf2));
        Assert.Equal(AdministrationService.AccountNotFound, wrongRole.Message);
        Assert.Equal(1, _audit.Events.Count(e => e == (AuditEvent.RecoveryCodeIssued, AccountRole.Admin)));
    }
}
