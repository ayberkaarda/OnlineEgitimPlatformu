using Moq;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Tests;

public sealed class EnrollmentServiceTests
{
    private static readonly CourseSummary DenizCourse = new(3, "Temel Programlama", 1, "Deniz Örnek", "Döngüler");

    private readonly Mock<IEnrollmentRepository> _enrollments = new(MockBehavior.Strict);
    private readonly Mock<ICourseRepository> _courses = new(MockBehavior.Strict);
    private readonly RecordingAudit _audit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private EnrollmentService Service => new(_enrollments.Object, _courses.Object, _audit);

    [Fact]
    public async Task Enroll_Twice_GetsTheOriginalMessage()
    {
        _courses.Setup(c => c.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(DenizCourse);
        _enrollments.Setup(e => e.AddAsync(5, 3, It.IsAny<CancellationToken>())).ThrowsAsync(new AlreadyEnrolledException());

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.EnrollAsync(Sessions.Student(5), 3, Ct));

        Assert.Equal("Bu dersi zaten seçtiniz.", error.Message);
        Assert.Empty(_audit.Events);
    }

    [Fact]
    public async Task Enroll_UsesTheSessionsStudent()
    {
        _courses.Setup(c => c.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(DenizCourse);
        _enrollments.Setup(e => e.AddAsync(5, 3, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await Service.EnrollAsync(Sessions.Student(5), 3, Ct);

        Assert.Equal([(AuditEvent.Enrolled, (AccountRole?)AccountRole.Student)], _audit.Events);
    }

    [Fact]
    public async Task Enroll_NoSelectionOrMissingCourse_IsRefusedWithoutAWrite()
    {
        _courses.Setup(c => c.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((CourseSummary?)null);

        var none = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.EnrollAsync(Sessions.Student(), 0, Ct));
        var missing = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.EnrollAsync(Sessions.Student(), 99, Ct));

        Assert.Equal(EnrollmentService.CourseRequired, none.Message);
        Assert.Equal(Access.CourseNotFound, missing.Message);
        _enrollments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Students_ForeignAndMissingCourse_SameAnswerAndNoNamesRead()
    {
        _courses.Setup(c => c.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(DenizCourse);
        _courses.Setup(c => c.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((CourseSummary?)null);

        var foreign = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.GetStudentsAsync(Sessions.Teacher(2), 3, Ct));
        var missing = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.GetStudentsAsync(Sessions.Teacher(2), 99, Ct));

        Assert.Equal(missing.Message, foreign.Message);
        _enrollments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WrongRole_IsDenied()
    {
        await Assert.ThrowsAsync<AccessDeniedException>(() => Service.EnrollAsync(Sessions.Teacher(), 3, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Service.GetMyCoursesAsync(Sessions.Teacher(), Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Service.GetStudentsAsync(Sessions.Student(), 3, Ct));
        _enrollments.VerifyNoOtherCalls();
        _courses.VerifyNoOtherCalls();
    }
}

public sealed class SetupServiceTests
{
    private const string Key = "0123-4567-89AB-CDEF-0123-4567-89AB-CDEF";

    private static readonly StoredSetupKey Stored = new(7, SetupKeyPolicy.Hash("0123456789ABCDEF0123456789ABCDEF"));

    private readonly Mock<IAccountRepository> _accounts = new(MockBehavior.Strict);
    private readonly PasswordHasher _hasher = new(iterations: 1_000);
    private readonly RecordingAudit _audit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private SetupService Service => new(
        _accounts.Object,
        _hasher,
        new SetupKeyGate(TimeProvider.System, new SetupKeyThrottle(TimeSpan.Zero)),
        new SetupKeyEnvironment(null),
        _audit);

    /// <summary>The repository checks the key against the stored one inside its transaction, as the real one does.</summary>
    private void RepositoryCreates(int id, Action<Teacher>? stored = null) =>
        _accounts.Setup(a => a.AddFirstTeacherAsync(It.IsAny<Teacher>(), It.IsAny<SetupKeyAttempt?>(), It.IsAny<CancellationToken>()))
            .Returns<Teacher, SetupKeyAttempt?, CancellationToken>((t, attempt, _) =>
            {
                stored?.Invoke(t);
                return Task.FromResult(attempt?.FindMatch([Stored]) == Stored.Id
                    ? new FirstTeacherResult(FirstTeacherStatus.Created, id)
                    : new FirstTeacherResult(FirstTeacherStatus.NotAuthorized, 0));
            });

    [Fact]
    public async Task Create_StoresAHashOfTheChosenPassword()
    {
        Teacher? stored = null;
        RepositoryCreates(1, t => stored = t);

        var session = await Service.CreateFirstTeacherAsync(Key, " Ada Kurucu ", " Matematik ", "Gizli.Sifre1", "Gizli.Sifre1", Ct);

        Assert.True(session.IsTeacher);
        Assert.NotNull(stored);
        Assert.Equal("Ada Kurucu", stored.FullName);
        Assert.Equal("Matematik", stored.Specialty);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify("Gizli.Sifre1", stored.PasswordHash, PasswordFormat.Pbkdf2));
        Assert.Equal([(AuditEvent.FirstTeacherCreated, (AccountRole?)AccountRole.Teacher)], _audit.Events);
    }

    [Fact]
    public async Task Create_WrongKey_IsRefusedAndAudited()
    {
        RepositoryCreates(1);

        foreach (var wrong in new[] { "YANLIS", Key[..^1] + "0", Key + "0", new string('-', 39) })
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(
                () => Service.CreateFirstTeacherAsync(wrong, "Ada Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", Ct));
            Assert.Equal(SetupService.SetupKeyInvalid, error.Message);
        }

        Assert.All(_audit.Events, e => Assert.Equal((AuditEvent.SetupRefused, (AccountRole?)null), e));
    }

    [Fact]
    public async Task Create_TeacherAlreadyExists_IsRefused()
    {
        _accounts.Setup(a => a.AddFirstTeacherAsync(It.IsAny<Teacher>(), It.IsAny<SetupKeyAttempt?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirstTeacherResult(FirstTeacherStatus.TeacherExists, 0));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service.CreateFirstTeacherAsync(Key, "Ada Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", Ct));

        Assert.Equal(SetupService.AlreadyDone, error.Message);
        Assert.Empty(_audit.Events);
    }

    [Theory]
    [InlineData(Key, null, "Matematik", "Gizli.Sifre1", "Gizli.Sifre1")]
    [InlineData(Key, "Ada Kurucu", " ", "Gizli.Sifre1", "Gizli.Sifre1")]
    [InlineData(Key, "Ada Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre2")]
    [InlineData(Key, "Ada Kurucu", "Matematik", "kisa", "kisa")]
    [InlineData(Key, "Ada<script>", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1")]
    public async Task Create_InvalidInput_IsRefusedBeforeAnyWrite(string? key, string? name, string? specialty, string password, string repeat)
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CreateFirstTeacherAsync(key, name, specialty, password, repeat, Ct));

        _accounts.VerifyNoOtherCalls();
    }
}

public sealed class SetupKeyTests
{
    /// <summary>
    /// The form of a key (32 hexadecimal characters) is checked after normalization: dashes and spaces
    /// do not make a short value long. A set but invalid EGITIM_SETUP_KEY is a configuration error
    /// and fills in nothing; it is never a key on its own.
    /// </summary>
    [Theory]
    [InlineData("--------------------------------")]
    [InlineData("A-------------------------------")]
    [InlineData("      kisa                      ")]
    [InlineData("0123 4567 89ab cdef 0123 4567 89ab cde")]
    [InlineData("0123-4567-89AB-CDEF-0123-4567-89AB-CDEG")]
    [InlineData("0123-4567-89AB-CDEF-0123-4567-89AB-CDEF-0")]
    public void EnvironmentKey_NormalizedLengthMustMeetMinimum(string configured)
    {
        Assert.True(configured.Length >= SetupKeyPolicy.KeyLength);

        var environment = new SetupKeyEnvironment(configured);

        Assert.True(environment.IsSet);
        Assert.False(environment.IsValid);
        Assert.Null(environment.Key);
        Assert.False(SetupKeyPolicy.TryNormalize(configured, out _));
    }

    [Theory]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData(" 0123-4567-89ab-cdef 0123-4567-89AB-cdef ")]
    public void EnvironmentKey_ValidAfterNormalization_FillsInTheGroupedKey(string configured)
    {
        var environment = new SetupKeyEnvironment(configured);

        Assert.True(environment.IsValid);
        Assert.Equal("0123-4567-89AB-CDEF-0123-4567-89AB-CDEF", environment.Key);
    }

    [Fact]
    public void EnvironmentKey_NotSet_IsNeitherValidNorAnError()
    {
        var environment = new SetupKeyEnvironment(null);

        Assert.False(environment.IsSet);
        Assert.Null(environment.Key);
    }

    [Fact]
    public void Attempt_ComparesWithEveryUsableKeyAndFindsTheMatchingOne()
    {
        var gate = new SetupKeyGate(TimeProvider.System, new SetupKeyThrottle(TimeSpan.Zero));
        StoredSetupKey[] stored =
        [
            new(1, SetupKeyPolicy.Hash("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")),
            new(2, SetupKeyPolicy.Hash("0123456789ABCDEF0123456789ABCDEF")),
        ];

        Assert.Equal(2, gate.CreateAttempt("0123-4567-89ab-cdef-0123-4567-89ab-cdef")!.FindMatch(stored));
        Assert.Null(gate.CreateAttempt("0123-4567-89ab-cdef-0123-4567-89ab-cdee")!.FindMatch(stored));
        Assert.Null(gate.CreateAttempt("-")!.FindMatch(stored));
        Assert.Null(gate.CreateAttempt(" "));
        Assert.Null(gate.CreateAttempt(null));
    }
}
