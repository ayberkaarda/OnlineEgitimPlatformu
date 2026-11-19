using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Tests;

/// <summary>
/// Critical recovery and setup paths with strict mocks: invalid input never reaches a repository or the
/// password hash, and every refusal of the database's decision has its fixed message and audit code.
/// </summary>
public sealed class ServiceMockTests
{
    private const string Key = "0123456789ABCDEF0123456789ABCDEF";
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAccountRepository> _accounts = new(MockBehavior.Strict);
    private readonly Mock<IPasswordHasher> _hasher = new(MockBehavior.Strict);
    private readonly RecordingAudit _audit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RecoveryService Recovery => new(
        _accounts.Object,
        _hasher.Object,
        new SetupKeyGate(TimeProvider.System, new SetupKeyThrottle(TimeSpan.Zero)),
        new FakeTimeProvider(Now),
        _audit,
        CurrentSessions.Instance,
        Validators.StudentName,
        Validators.NewPassword);

    [Fact]
    public void AddBusinessServices_ResolvesEveryServiceWithTheCentralValidators()
    {
        var services = new ServiceCollection()
            .AddSingleton(_accounts.Object)
            .AddSingleton(Mock.Of<ICourseRepository>())
            .AddSingleton(Mock.Of<IEnrollmentRepository>())
            .AddSingleton(Mock.Of<IAccountAdministrationRepository>())
            .AddBusinessServices();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider.GetRequiredService<IAuthService>());
        Assert.NotNull(provider.GetRequiredService<ISetupService>());
        Assert.NotNull(provider.GetRequiredService<IRecoveryService>());
        Assert.NotNull(provider.GetRequiredService<ICourseService>());
        Assert.NotNull(provider.GetRequiredService<IEnrollmentService>());
        Assert.NotNull(provider.GetRequiredService<IAdministrationService>());
        Assert.IsType<RegistrationInputValidator>(provider.GetRequiredService<FluentValidation.IValidator<RegistrationInput>>());
        Assert.IsType<NoteInputValidator>(provider.GetRequiredService<FluentValidation.IValidator<NoteInput>>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task IssueCode_MissingName_IsRefusedBeforeAnyWrite(string? name)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Recovery.IssueStudentCodeAsync(Sessions.Teacher(), name, Ct));

        Assert.Equal(ValidationMessages.StudentNameRequired, error.Message);
        _accounts.VerifyNoOtherCalls();
        _hasher.VerifyNoOtherCalls();
        Assert.Empty(_audit.Events);
    }

    [Fact]
    public async Task IssueCode_Student_IsDeniedBeforeTheInputIsLookedAt()
    {
        await Assert.ThrowsAsync<AccessDeniedException>(() => Recovery.IssueStudentCodeAsync(Sessions.Student(), null, Ct));

        Assert.Equal([(AuditEvent.AccessDenied, (AccountRole?)AccountRole.Student)], _audit.Events);
        _accounts.VerifyNoOtherCalls();
    }

    public static TheoryData<string?, string?, string?, string?, string> InvalidFirstPasswords => new()
    {
        { null, "Ada Öğretmen", "Yeni.Sifre1", "Yeni.Sifre1", ValidationMessages.InputRequired },
        { Key, " ", "Yeni.Sifre1", "Yeni.Sifre1", ValidationMessages.InputRequired },
        { new string('A', 129), "Ada Öğretmen", "Yeni.Sifre1", "Yeni.Sifre1", ValidationMessages.InputRequired },
        { Key, new string('a', 101), "Yeni.Sifre1", "Yeni.Sifre1", ValidationMessages.NameInvalid },
        { Key, "Ada Öğretmen", "Yeni.Sifre1", "Yeni.Sifre2", ValidationMessages.PasswordsDoNotMatch },
        { Key, "Ada Öğretmen", "kisa", "kisa", ValidationMessages.PasswordInvalid },
    };

    [Theory]
    [MemberData(nameof(InvalidFirstPasswords))]
    public async Task RecoverTeacher_InvalidInput_TouchesNeitherTheDatabaseNorTheHash(string? key, string? name, string? password, string? repeat, string message)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Recovery.RecoverTeacherAsync(key, name, password, repeat, Ct));

        Assert.Equal(message, error.Message);
        _accounts.VerifyNoOtherCalls();
        _hasher.VerifyNoOtherCalls();
        Assert.Empty(_audit.Events);
    }

    [Theory]
    [InlineData(SetupKeyWriteStatus.NotAuthorized)]
    [InlineData(SetupKeyWriteStatus.NoSuchAccount)]
    public async Task RecoverStudentWithKey_RefusedWrite_OneMessageOneAuditNoSession(SetupKeyWriteStatus status)
    {
        _hasher.Setup(h => h.Hash("Yeni.Sifre1")).Returns("hash");
        _accounts.Setup(a => a.SetFirstPasswordWithSetupKeyAsync(AccountRole.Student, "Kayıtlı Öğrenci", "hash", It.IsAny<SetupKeyAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SetupKeyWriteResult(status, 0));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Recovery.RecoverStudentAsync(" Kayıtlı Öğrenci ", Key.ToLowerInvariant(), "Yeni.Sifre1", "Yeni.Sifre1", Ct));

        Assert.Equal(RecoveryService.SetupKeyRecoveryFailed, error.Message);
        Assert.Equal([(AuditEvent.RecoveryFailed, (AccountRole?)null)], _audit.Events);
        _accounts.Verify(a => a.FindRecoveryAsync(AccountRole.Student, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecoverTeacher_TeacherWithPasswordExists_KeyIsNotAccepted()
    {
        _hasher.Setup(h => h.Hash("Yeni.Sifre1")).Returns("hash");
        _accounts.Setup(a => a.SetFirstPasswordWithSetupKeyAsync(AccountRole.Teacher, "Ada Öğretmen", "hash", It.IsAny<SetupKeyAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SetupKeyWriteResult(SetupKeyWriteStatus.SetupCompleted, 0));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Recovery.RecoverTeacherAsync(Key, "Ada Öğretmen", "Yeni.Sifre1", "Yeni.Sifre1", Ct));

        Assert.Equal(SetupService.SetupKeyNotAllowed, error.Message);
        Assert.Equal([(AuditEvent.RecoveryFailed, (AccountRole?)null)], _audit.Events);
    }

    [Fact]
    public async Task RecoverTeacher_AcceptedKey_SignsInTheStoredAccount()
    {
        _hasher.Setup(h => h.Hash("Yeni.Sifre1")).Returns("hash");
        _accounts.Setup(a => a.SetFirstPasswordWithSetupKeyAsync(AccountRole.Teacher, "Ada Öğretmen", "hash", It.IsAny<SetupKeyAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SetupKeyWriteResult(SetupKeyWriteStatus.Done, 5));
        _accounts.Setup(a => a.GetTeacherAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new TeacherProfile(5, "Ada Öğretmen", "Fizik"));
        _accounts.Setup(a => a.GetSessionStateAsync(AccountRole.Teacher, 5, It.IsAny<CancellationToken>())).ReturnsAsync(new SessionState(AccountRole.Teacher, false, 0));

        var session = await Recovery.RecoverTeacherAsync(Key, "Ada Öğretmen", "Yeni.Sifre1", "Yeni.Sifre1", Ct);

        Assert.True(session.IsTeacher);
        Assert.Equal(5, session.UserId);
        Assert.Equal([(AuditEvent.AccountRecovered, (AccountRole?)AccountRole.Teacher)], _audit.Events);
    }
}
