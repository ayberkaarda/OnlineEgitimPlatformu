using Microsoft.Extensions.Time.Testing;
using Moq;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Tests;

public sealed class RecoveryServiceTests
{
    private const string WrongCode = "ZZZZZ-ZZZZZ";
    private const string IssuedCode = "ABCDE-FGHJK";

    private static readonly DateTimeOffset Now = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAccountRepository> _accounts = new(MockBehavior.Strict);
    private readonly CountingHasher _hasher = new();
    private readonly RecordingAudit _audit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RecoveryService Service => new(
        _accounts.Object,
        _hasher,
        new SetupKeyGate(TimeProvider.System, new SetupKeyThrottle(TimeSpan.Zero)),
        new FakeTimeProvider(Now),
        _audit,
        Validators.StudentName,
        Validators.NewPassword);

    public static TheoryData<string> RefusedStates => ["unknown", "no code", "expired", "locked", "wrong code", "has password"];

    /// <summary>
    /// Every refused recovery spends exactly one code verification, real or simulated, and gets the
    /// same message: the duration does not tell an unknown name from an account with an expired, a
    /// missing or a wrong code, a locked account or one that has a password.
    /// </summary>
    [Theory]
    [MemberData(nameof(RefusedStates))]
    public async Task RecoveryFailure_PerformsOneVerificationWorkUnit(string state)
    {
        var issued = _hasher.Inner.Hash(CodeFormat.Normalize(IssuedCode));
        var now = Now.UtcDateTime;
        StudentRecoveryState? student = state switch
        {
            "unknown" => null,
            "no code" => new(4, "Şifresiz Öğrenci", PasswordFormat.None, null, null, null),
            "expired" => new(4, "Şifresiz Öğrenci", PasswordFormat.None, issued, now, null),
            "locked" => new(4, "Şifresiz Öğrenci", PasswordFormat.None, issued, now.AddHours(1), now.AddMinutes(5)),
            "wrong code" => new(4, "Şifresiz Öğrenci", PasswordFormat.None, issued, now.AddHours(1), null),
            "has password" => new(4, "Şifresiz Öğrenci", PasswordFormat.Pbkdf2, null, null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
        _accounts.Setup(a => a.FindStudentRecoveryAsync("Şifresiz Öğrenci", It.IsAny<CancellationToken>())).ReturnsAsync(student);
        _accounts.Setup(a => a.RecordFailedLoginAsync(AccountRole.Student, 4, LoginPolicy.MaxFailedAttempts, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service.RecoverStudentAsync("Şifresiz Öğrenci", state == "wrong code" ? WrongCode : IssuedCode, "Yeni.Sifre1", "Yeni.Sifre1", Ct));

        Assert.Equal(RecoveryService.StudentRecoveryFailed, error.Message);
        Assert.Equal(1, _hasher.WorkUnits);
        _accounts.Verify(a => a.CompleteStudentRecoveryAsync(It.IsAny<StudentRecoveryState>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Recovery_RightCode_VerifiesOnceAndCompletes()
    {
        var issued = _hasher.Inner.Hash(CodeFormat.Normalize(IssuedCode));
        var student = new StudentRecoveryState(4, "Şifresiz Öğrenci", PasswordFormat.None, issued, Now.UtcDateTime.AddHours(1), null);
        _accounts.Setup(a => a.FindStudentRecoveryAsync("Şifresiz Öğrenci", It.IsAny<CancellationToken>())).ReturnsAsync(student);
        _accounts.Setup(a => a.CompleteStudentRecoveryAsync(student, Now.UtcDateTime, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var session = await Service.RecoverStudentAsync("Şifresiz Öğrenci", IssuedCode.ToLowerInvariant(), "Yeni.Sifre1", "Yeni.Sifre1", Ct);

        Assert.Equal(4, session.UserId);
        Assert.Equal(1, _hasher.WorkUnits);
    }

    /// <summary>Counts the code verifications, real and simulated; hashing the new password is not one.</summary>
    private sealed class CountingHasher : IPasswordHasher
    {
        public PasswordHasher Inner { get; } = new(iterations: 1_000);

        public int WorkUnits { get; private set; }

        public string Hash(string password) => Inner.Hash(password);

        public PasswordVerificationResult Verify(string password, string stored, PasswordFormat format)
        {
            WorkUnits++;
            return Inner.Verify(password, stored, format);
        }

        public void SimulateVerification(string password)
        {
            WorkUnits++;
            Inner.SimulateVerification(password);
        }
    }
}
