using Microsoft.Extensions.Time.Testing;
using Moq;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Tests;

/// <summary>The administrators' account management and the session guard, with strict mocks.</summary>
public sealed class AdministrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAccountRepository> _accounts = new(MockBehavior.Strict);
    private readonly Mock<IAccountAdministrationRepository> _administration = new(MockBehavior.Strict);
    private readonly PasswordHasher _hasher = new(iterations: 1_000);
    private readonly RecordingAudit _audit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AdministrationService Service => new(
        _accounts.Object,
        _administration.Object,
        _hasher,
        new FakeTimeProvider(Now),
        _audit,
        CurrentSessions.Instance,
        new TeacherAccountInputValidator(),
        new StudentAccountInputValidator(),
        new AccountSearchValidator());

    public static TheoryData<UserSession> NotAdmins => new() { Sessions.Student(1), Sessions.Teacher(1) };

    /// <summary>Authorization is the service's: a student or teacher session reaches no repository at all.</summary>
    [Theory]
    [MemberData(nameof(NotAdmins))]
    public async Task EveryOperation_RefusesStudentsAndTeachersBeforeAnyRepository(UserSession session)
    {
        await Assert.ThrowsAsync<AccessDeniedException>(() => Service.SearchAccountsAsync(session, new AccountSearch(), Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Service.CreateTeacherAsync(session, "Yeni Öğretmen", "Fizik", Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Service.CreateStudentAsync(session, "Yeni Öğrenci", "yeni@example.com", Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Service.SetDisabledAsync(session, AccountRole.Student, 2, true, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Service.DemoteAdminAsync(session, 2, Ct));

        Assert.Equal(5, _audit.Events.Count(e => e.Event == AuditEvent.AccessDenied));
        _accounts.VerifyNoOtherCalls();
        _administration.VerifyNoOtherCalls();
    }

    /// <summary>
    /// A new teacher gets no password: the stored hash belongs to the returned code, which works for
    /// 24 hours. The code itself is never stored.
    /// </summary>
    [Fact]
    public async Task CreateTeacher_StoresOnlyTheHashOfTheReturnedCode()
    {
        Teacher? stored = null;
        _administration.Setup(a => a.AddTeacherAsync(It.IsAny<Teacher>(), It.IsAny<CancellationToken>()))
            .Callback<Teacher, CancellationToken>((t, _) => stored = t)
            .ReturnsAsync(9);

        var code = await Service.CreateTeacherAsync(Sessions.Admin(), " Yeni Öğretmen ", " Fizik ", Ct);

        Assert.NotNull(stored);
        Assert.Equal(("Yeni Öğretmen", "Fizik", PasswordFormat.None, AccountRole.Teacher), (stored.FullName, stored.Specialty, stored.PasswordFormat, stored.Role));
        Assert.Empty(stored.PasswordHash);
        Assert.DoesNotContain(CodeFormat.Normalize(code.Code), stored.RecoveryCodeHash!, StringComparison.Ordinal);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify(CodeFormat.Normalize(code.Code), stored.RecoveryCodeHash!, PasswordFormat.Pbkdf2));
        Assert.Equal(Now.UtcDateTime + RecoveryPolicy.CodeLifetime, code.ExpiresUtc);
        Assert.Equal(code.ExpiresUtc, stored.RecoveryCodeExpires);
        Assert.Equal([(AuditEvent.AccountCreated, (AccountRole?)AccountRole.Teacher)], _audit.Events);
    }

    [Fact]
    public async Task CreateTeacher_TakenNameOrInvalidInput_IsRefused()
    {
        _administration.Setup(a => a.AddTeacherAsync(It.IsAny<Teacher>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateAccountException());

        var taken = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CreateTeacherAsync(Sessions.Admin(), "Deniz Örnek", "Fizik", Ct));
        var missing = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CreateTeacherAsync(Sessions.Admin(), "Deniz Örnek", " ", Ct));
        var name = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CreateTeacherAsync(Sessions.Admin(), "Deniz<script>", "Fizik", Ct));

        Assert.Equal(AdministrationService.TeacherNameTaken, taken.Message);
        Assert.Equal(ValidationMessages.InputRequired, missing.Message);
        Assert.Equal(ValidationMessages.NameInvalid, name.Message);
        _administration.Verify(a => a.AddTeacherAsync(It.IsAny<Teacher>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(_audit.Events);
    }

    [Fact]
    public async Task CreateStudent_TakenNameOrAddressIsNamedToTheAdministrator()
    {
        _accounts.Setup(a => a.StudentExistsAsync("Mert Deneme", "mert@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var taken = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CreateStudentAsync(Sessions.Admin(), "Mert Deneme", "mert@example.com", Ct));
        var mail = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CreateStudentAsync(Sessions.Admin(), "Mert Deneme", "gecersiz", Ct));

        Assert.Equal(AdministrationService.StudentTaken, taken.Message);
        Assert.Equal(ValidationMessages.EmailInvalid, mail.Message);
        _accounts.Verify(a => a.AddStudentAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateStudent_StoresNoPasswordAndTheCodesHash()
    {
        Student? stored = null;
        _accounts.Setup(a => a.StudentExistsAsync("Yeni Öğrenci", "yeni@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _accounts.Setup(a => a.AddStudentAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()))
            .Callback<Student, CancellationToken>((s, _) => stored = s)
            .ReturnsAsync(5);

        var code = await Service.CreateStudentAsync(Sessions.Admin(), "Yeni Öğrenci", "yeni@example.com", Ct);

        Assert.Equal(PasswordFormat.None, stored!.PasswordFormat);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify(CodeFormat.Normalize(code.Code), stored.RecoveryCodeHash!, PasswordFormat.Pbkdf2));
        Assert.Equal([(AuditEvent.AccountCreated, (AccountRole?)AccountRole.Student)], _audit.Events);
    }

    [Fact]
    public async Task SetDisabled_LastActiveAdminOrMissingAccount_HaveTheirMessages()
    {
        _administration.Setup(a => a.SetDisabledAsync(AccountRole.Admin, 1, true, It.IsAny<CancellationToken>())).ReturnsAsync(AccountWriteStatus.LastActiveAdmin);
        _administration.Setup(a => a.SetDisabledAsync(AccountRole.Student, 99, true, It.IsAny<CancellationToken>())).ReturnsAsync(AccountWriteStatus.NotFound);
        _administration.Setup(a => a.DemoteAdminAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(AccountWriteStatus.LastActiveAdmin);

        var last = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.SetDisabledAsync(Sessions.Admin(), AccountRole.Admin, 1, true, Ct));
        var demote = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.DemoteAdminAsync(Sessions.Admin(), 1, Ct));
        var missing = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.SetDisabledAsync(Sessions.Admin(), AccountRole.Student, 99, true, Ct));
        var role = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.SetDisabledAsync(Sessions.Admin(), (AccountRole)9, 1, true, Ct));
        var id = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.DemoteAdminAsync(Sessions.Admin(), 0, Ct));

        Assert.Equal(AdministrationService.LastActiveAdmin, last.Message);
        Assert.Equal(AdministrationService.LastActiveAdmin, demote.Message);
        Assert.All(new[] { missing, role, id }, e => Assert.Equal(AdministrationService.AccountNotFound, e.Message));
        Assert.Empty(_audit.Events);
    }

    [Theory]
    [InlineData(true, AuditEvent.AccountDisabled)]
    [InlineData(false, AuditEvent.AccountEnabled)]
    public async Task SetDisabled_Done_IsAuditedWithTheAccountsRole(bool disabled, AuditEvent expected)
    {
        _administration.Setup(a => a.SetDisabledAsync(AccountRole.Teacher, 4, disabled, It.IsAny<CancellationToken>())).ReturnsAsync(AccountWriteStatus.Done);

        await Service.SetDisabledAsync(Sessions.Admin(), AccountRole.Teacher, 4, disabled, Ct);

        Assert.Equal([(expected, (AccountRole?)AccountRole.Teacher)], _audit.Events);
    }

    [Fact]
    public async Task Search_CleansTheTextAndPages()
    {
        AccountSearchCriteria? asked = null;
        _administration.Setup(a => a.SearchAsync(It.IsAny<AccountSearchCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<AccountSearchCriteria, CancellationToken>((c, _) => asked = c)
            .ReturnsAsync(new AccountPage([new AccountListing(AccountRole.Student, 1, "Mert Deneme", "mert@example.com", false, true)], 21));

        var result = await Service.SearchAccountsAsync(Sessions.Admin(), new AccountSearch(" mert ", AccountRole.Student, 1), Ct);

        Assert.Equal(new AccountSearchCriteria("mert", AccountRole.Student, 20, 20), asked);
        Assert.Equal((1, 2), (result.PageIndex, result.PageCount));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service.SearchAccountsAsync(Sessions.Admin(), new AccountSearch(PageSize: 51), Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service.SearchAccountsAsync(Sessions.Admin(), new AccountSearch(Role: (AccountRole)7), Ct));
    }

    /// <summary>The session guard refuses a session whose account is gone, disabled, has another role or another version.</summary>
    [Theory]
    [InlineData(false, false, AccountRole.Admin, 3, true)]
    [InlineData(true, false, AccountRole.Admin, 3, false)]
    [InlineData(false, true, AccountRole.Admin, 3, false)]
    [InlineData(false, false, AccountRole.Teacher, 3, false)]
    [InlineData(false, false, AccountRole.Admin, 4, false)]
    public async Task SessionGuard_AcceptsOnlyAnUnchangedAccount(bool missing, bool disabled, AccountRole storedRole, int storedVersion, bool accepted)
    {
        _accounts.Setup(a => a.GetSessionStateAsync(AccountRole.Admin, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(missing ? null : new SessionState(storedRole, disabled, storedVersion));
        var guard = new SessionGuard(_accounts.Object, _audit);

        var check = guard.EnsureCurrentAsync(Sessions.Admin(7, version: 3), Ct);

        if (accepted)
        {
            await check;
            Assert.Empty(_audit.Events);
        }
        else
        {
            var error = await Assert.ThrowsAsync<SessionExpiredException>(() => check);
            Assert.Equal(SessionExpiredException.DefaultMessage, error.Message);
            Assert.Equal([(AuditEvent.SessionRejected, (AccountRole?)AccountRole.Admin)], _audit.Events);
        }
    }

    /// <summary>Course views are not for administrators: their id is a staff id and is never compared with student ids.</summary>
    [Fact]
    public async Task CourseDetails_AdministratorIsDenied()
    {
        var courses = new Mock<ICourseRepository>(MockBehavior.Strict);
        var enrollments = new Mock<IEnrollmentRepository>(MockBehavior.Strict);
        var service = new CourseService(courses.Object, enrollments.Object, _accounts.Object, new FakeTimeProvider(Now), _audit, CurrentSessions.Instance, Validators.Course, Validators.Note, Validators.CourseSearch);

        await Assert.ThrowsAsync<AccessDeniedException>(() => service.GetDetailsAsync(Sessions.Admin(3), 3, Ct));

        courses.VerifyNoOtherCalls();
        enrollments.VerifyNoOtherCalls();
    }

    /// <summary>Every service checks the session before it reads or writes anything.</summary>
    [Fact]
    public async Task ExpiredSession_StopsEveryServiceBeforeTheRepositories()
    {
        var courses = new Mock<ICourseRepository>(MockBehavior.Strict);
        var enrollments = new Mock<IEnrollmentRepository>(MockBehavior.Strict);
        var expired = new ExpiredSessions();
        var clock = new FakeTimeProvider(Now);
        var courseService = new CourseService(courses.Object, enrollments.Object, _accounts.Object, clock, _audit, expired, Validators.Course, Validators.Note, Validators.CourseSearch);
        var enrollmentService = new EnrollmentService(enrollments.Object, courses.Object, clock, _audit, expired);
        var admin = new AdministrationService(_accounts.Object, _administration.Object, _hasher, clock, _audit, expired, new TeacherAccountInputValidator(), new StudentAccountInputValidator(), new AccountSearchValidator());

        await Assert.ThrowsAsync<SessionExpiredException>(() => courseService.GetCatalogAsync(Sessions.Student(), Ct));
        await Assert.ThrowsAsync<SessionExpiredException>(() => courseService.SearchCatalogAsync(Sessions.Student(), new CourseSearch(), Ct));
        await Assert.ThrowsAsync<SessionExpiredException>(() => courseService.GetDetailsAsync(Sessions.Student(), 3, Ct));
        await Assert.ThrowsAsync<SessionExpiredException>(() => courseService.CreateAsync(Sessions.Teacher(), "Ders", "Konu", null, Ct));
        await Assert.ThrowsAsync<SessionExpiredException>(() => courseService.DeleteAsync(Sessions.Teacher(), 3, 0, Ct));
        await Assert.ThrowsAsync<SessionExpiredException>(() => courseService.AddNoteAsync(Sessions.Teacher(), 3, "Not", Ct));
        await Assert.ThrowsAsync<SessionExpiredException>(() => enrollmentService.EnrollAsync(Sessions.Student(), 3, Ct));
        await Assert.ThrowsAsync<SessionExpiredException>(() => enrollmentService.LeaveAsync(Sessions.Student(), 3, Ct));
        await Assert.ThrowsAsync<SessionExpiredException>(() => enrollmentService.GetStudentsAsync(Sessions.Teacher(), 3, Ct));
        await Assert.ThrowsAsync<SessionExpiredException>(() => admin.SearchAccountsAsync(Sessions.Admin(), new AccountSearch(), Ct));
        await Assert.ThrowsAsync<SessionExpiredException>(() => admin.CreateStudentAsync(Sessions.Admin(), "Yeni Öğrenci", "yeni@example.com", Ct));

        courses.VerifyNoOtherCalls();
        enrollments.VerifyNoOtherCalls();
        _accounts.VerifyNoOtherCalls();
        _administration.VerifyNoOtherCalls();
    }

    private sealed class ExpiredSessions : ISessionGuard
    {
        public Task EnsureCurrentAsync(UserSession session, CancellationToken cancellationToken) => throw new SessionExpiredException();
    }
}
