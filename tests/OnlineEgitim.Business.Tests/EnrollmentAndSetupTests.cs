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
    private readonly Mock<IAccountRepository> _accounts = new(MockBehavior.Strict);
    private readonly PasswordHasher _hasher = new(iterations: 1_000);
    private readonly RecordingAudit _audit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private SetupService Service => new(_accounts.Object, _hasher, _audit);

    [Fact]
    public async Task Create_StoresAHashOfTheChosenPassword()
    {
        Teacher? stored = null;
        _accounts.Setup(a => a.AddFirstTeacherAsync(It.IsAny<Teacher>(), It.IsAny<CancellationToken>()))
            .Callback<Teacher, CancellationToken>((t, _) => stored = t)
            .ReturnsAsync(1);

        var session = await Service.CreateFirstTeacherAsync(" Ada Kurucu ", " Matematik ", "Gizli.Sifre1", "Gizli.Sifre1", Ct);

        Assert.True(session.IsTeacher);
        Assert.NotNull(stored);
        Assert.Equal("Ada Kurucu", stored.FullName);
        Assert.Equal("Matematik", stored.Specialty);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify("Gizli.Sifre1", stored.PasswordHash, PasswordFormat.Pbkdf2));
        Assert.Equal([(AuditEvent.FirstTeacherCreated, (AccountRole?)AccountRole.Teacher)], _audit.Events);
    }

    [Fact]
    public async Task Create_TeacherAlreadyExists_IsRefused()
    {
        _accounts.Setup(a => a.AddFirstTeacherAsync(It.IsAny<Teacher>(), It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service.CreateFirstTeacherAsync("Ada Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", Ct));

        Assert.Equal(SetupService.AlreadyDone, error.Message);
        Assert.Empty(_audit.Events);
    }

    [Theory]
    [InlineData(null, "Matematik", "Gizli.Sifre1", "Gizli.Sifre1")]
    [InlineData("Ada Kurucu", " ", "Gizli.Sifre1", "Gizli.Sifre1")]
    [InlineData("Ada Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre2")]
    [InlineData("Ada Kurucu", "Matematik", "kisa", "kisa")]
    [InlineData("Ada<script>", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1")]
    public async Task Create_InvalidInput_IsRefusedBeforeAnyWrite(string? name, string? specialty, string password, string repeat)
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CreateFirstTeacherAsync(name, specialty, password, repeat, Ct));

        _accounts.VerifyNoOtherCalls();
    }
}
