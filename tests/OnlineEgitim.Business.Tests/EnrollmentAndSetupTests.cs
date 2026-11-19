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
    private const string Key = "AAAAA-BBBBB-CCCCC-DDDDD";

    private readonly Mock<IAccountRepository> _accounts = new(MockBehavior.Strict);
    private readonly Mock<ISetupKeyStore> _keys = new(MockBehavior.Strict);
    private readonly PasswordHasher _hasher = new(iterations: 1_000);
    private readonly RecordingAudit _audit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private SetupService Service => new(_accounts.Object, _hasher, _keys.Object, _audit);

    /// <summary>The repository runs the authorization inside its transaction, as the real one does.</summary>
    private void RepositoryCreates(int id, Action<Teacher>? stored = null) =>
        _accounts.Setup(a => a.AddFirstTeacherAsync(It.IsAny<Teacher>(), It.IsAny<Func<bool>>(), It.IsAny<CancellationToken>()))
            .Returns<Teacher, Func<bool>, CancellationToken>((t, authorize, _) =>
            {
                stored?.Invoke(t);
                return Task.FromResult(authorize()
                    ? new FirstTeacherResult(FirstTeacherStatus.Created, id)
                    : new FirstTeacherResult(FirstTeacherStatus.NotAuthorized, 0));
            });

    [Fact]
    public async Task Create_StoresAHashOfTheChosenPassword()
    {
        Teacher? stored = null;
        RepositoryCreates(1, t => stored = t);
        _keys.Setup(k => k.TryConsume(Key)).Returns(true);

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
        _keys.Setup(k => k.TryConsume("YANLIS")).Returns(false);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service.CreateFirstTeacherAsync("YANLIS", "Ada Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", Ct));

        Assert.Equal(SetupService.SetupKeyInvalid, error.Message);
        Assert.Equal([(AuditEvent.SetupRefused, (AccountRole?)null)], _audit.Events);
    }

    [Fact]
    public async Task Create_TeacherAlreadyExists_IsRefused()
    {
        _accounts.Setup(a => a.AddFirstTeacherAsync(It.IsAny<Teacher>(), It.IsAny<Func<bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirstTeacherResult(FirstTeacherStatus.TeacherExists, 0));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service.CreateFirstTeacherAsync(Key, "Ada Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", Ct));

        Assert.Equal(SetupService.AlreadyDone, error.Message);
        Assert.Empty(_audit.Events);
        _keys.VerifyNoOtherCalls(); // the key is not used up
    }

    [Theory]
    [InlineData(Key, null, "Matematik", "Gizli.Sifre1", "Gizli.Sifre1")]
    [InlineData(Key, "Ada Kurucu", " ", "Gizli.Sifre1", "Gizli.Sifre1")]
    [InlineData(Key, "Ada Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre2")]
    [InlineData(Key, "Ada Kurucu", "Matematik", "kisa", "kisa")]
    [InlineData(Key, "Ada<script>", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1")]
    [InlineData(" ", "Ada Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1")]
    public async Task Create_InvalidInput_IsRefusedBeforeAnyWrite(string? key, string? name, string? specialty, string password, string repeat)
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CreateFirstTeacherAsync(key, name, specialty, password, repeat, Ct));

        _accounts.VerifyNoOtherCalls();
        _keys.VerifyNoOtherCalls();
    }
}

public sealed class FileSetupKeyStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "OnlineEgitimKeyTest_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void FileKey_WorksOnceAndIsDeleted()
    {
        var store = new FileSetupKeyStore(_directory, environmentKey: null);
        store.EnsureIssued();
        var path = Assert.IsType<string>(store.KeyFilePath);
        var key = File.ReadAllText(path).Trim();
        store.EnsureIssued(); // keeps the issued key

        Assert.Matches("^[0-9A-Z]{5}(-[0-9A-Z]{5}){3}$", key);
        Assert.Equal(key, File.ReadAllText(path).Trim());
        Assert.False(store.TryConsume("YANLIS-" + key));
        Assert.False(store.TryConsume(null));
        Assert.True(File.Exists(path));
        Assert.True(store.TryConsume(" " + key.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal) + " "));
        Assert.False(File.Exists(path));
        Assert.False(store.TryConsume(key));
        Assert.Empty(Directory.GetFiles(_directory));

        store.EnsureIssued(); // a new key for the next founder operation
        Assert.NotEqual(key, File.ReadAllText(path).Trim());
    }

    [Fact]
    public void FileKey_ConcurrentConsumers_ExactlyOneWins()
    {
        var store = new FileSetupKeyStore(_directory, environmentKey: null);
        store.EnsureIssued();
        var key = File.ReadAllText(store.KeyFilePath!).Trim();

        var wins = 0;
        Parallel.For(0, 16, _ =>
        {
            if (new FileSetupKeyStore(_directory, environmentKey: null).TryConsume(key))
            {
                Interlocked.Increment(ref wins);
            }
        });

        Assert.Equal(1, wins);
    }

    [Fact]
    public void EnvironmentKey_WorksOnceAndWritesNoKeyFile()
    {
        const string Key = "ortam-anahtari-1234567";
        var store = new FileSetupKeyStore(_directory, Key);
        store.EnsureIssued();

        Assert.Null(store.KeyFilePath);
        Assert.False(store.TryConsume("ortam-anahtari-1234568"));
        Assert.True(store.TryConsume(Key));
        Assert.False(store.TryConsume(Key));
        Assert.False(new FileSetupKeyStore(_directory, Key).TryConsume(Key));
        Assert.All(Directory.GetFiles(_directory), f => Assert.DoesNotContain("ortam", File.ReadAllText(f), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnvironmentKey_TooShort_IsIgnored()
    {
        var store = new FileSetupKeyStore(_directory, "kisa");

        Assert.NotNull(store.KeyFilePath);
        Assert.False(store.TryConsume("kisa"));
    }
}
