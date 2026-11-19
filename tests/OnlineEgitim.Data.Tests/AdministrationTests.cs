using Microsoft.EntityFrameworkCore;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Domain.Entities;
using static OnlineEgitim.Data.Tests.TestServices;

namespace OnlineEgitim.Data.Tests;

/// <summary>
/// Account management end to end on SQLite in-memory: accounts created with a single use code, disabled
/// accounts and their sessions, the last administrator and the account list.
/// </summary>
public sealed class AdministrationTests : IAsyncLifetime
{
    private const string AdminName = "Ayla Yönetici";
    private const string AdminPassword = "Gizli.Yonetici1";

    private readonly SqliteDatabase _db = new();
    private UserSession _admin = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IAdministrationService Admin => _db.Get<IAdministrationService>();

    private IAuthService Auth => _db.Get<IAuthService>();

    public async ValueTask InitializeAsync()
    {
        await _db.InitializeAsync();
        await AddAdminAsync(AdminName);
        _admin = await Auth.SignInTeacherAsync(AdminName, AdminPassword, Ct);
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task AdminSignIn_GetsTheAdministratorRole()
    {
        Assert.True(_admin.IsAdmin);
        await Assert.ThrowsAsync<AccessDeniedException>(() => _db.Get<ICourseService>().GetTeachingAsync(_admin, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => _db.Get<ICourseService>().GetDetailsAsync(_admin, DenizCourseId, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Admin.SearchAccountsAsync(Teacher(DenizId), new AccountSearch(), Ct));
    }

    /// <summary>
    /// A teacher created by an administrator has no password: no password signs in, the single use code
    /// sets one, once, and the account then signs in as a teacher.
    /// </summary>
    [Fact]
    public async Task CreatedTeacher_SetsTheFirstPasswordWithTheCodeOnce()
    {
        var code = await Admin.CreateTeacherAsync(_admin, "Yeni Öğretmen", "Kimya", Ct);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInTeacherAsync("Yeni Öğretmen", code.Code, Ct));

        var recovery = _db.Get<IRecoveryService>();
        var session = await recovery.RecoverTeacherAsync(code.Code.ToLowerInvariant(), "Yeni Öğretmen", "Yeni.Sifre1", "Yeni.Sifre1", Ct);
        var again = await Assert.ThrowsAsync<BusinessRuleException>(
            () => recovery.RecoverTeacherAsync(code.Code, "Yeni Öğretmen", "Baska.Sifre1", "Baska.Sifre1", Ct));

        Assert.True(session.IsTeacher);
        Assert.Equal(RecoveryService.StudentRecoveryFailed, again.Message);
        Assert.Equal(session.UserId, (await Auth.SignInTeacherAsync("Yeni Öğretmen", "Yeni.Sifre1", Ct)).UserId);
        Assert.Empty(await _db.Get<ICourseService>().GetTeachingAsync(session, Ct));
    }

    [Fact]
    public async Task CreatedStudent_SetsTheFirstPasswordWithTheCode()
    {
        var code = await Admin.CreateStudentAsync(_admin, "Yeni Öğrenci", "yeni@example.com", Ct);

        var session = await _db.Get<IRecoveryService>().RecoverStudentAsync("Yeni Öğrenci", code.Code, "Yeni.Sifre1", "Yeni.Sifre1", Ct);

        Assert.True(session.IsStudent);
        Assert.Equal(session.UserId, (await Auth.SignInStudentAsync("Yeni Öğrenci", "Yeni.Sifre1", Ct)).UserId);
        var taken = await Assert.ThrowsAsync<BusinessRuleException>(() => Admin.CreateStudentAsync(_admin, "Başka Ad", "yeni@example.com", Ct));
        var teacherTaken = await Assert.ThrowsAsync<BusinessRuleException>(() => Admin.CreateTeacherAsync(_admin, "Deniz Örnek", "Fizik", Ct));
        Assert.Equal(AdministrationService.StudentTaken, taken.Message);
        Assert.Equal(AdministrationService.TeacherNameTaken, teacherTaken.Message);
    }

    /// <summary>
    /// A disabled account is refused at sign-in like a wrong password, its open session ends, and
    /// enabling it again lets it sign in but does not revive the old session.
    /// </summary>
    [Fact]
    public async Task DisabledAccount_CannotSignInAndItsSessionEnds()
    {
        var student = await Auth.SignInStudentAsync("Mert Deneme", StudentPassword, Ct);
        var courses = _db.Get<ICourseService>();
        Assert.NotEmpty(await courses.GetCatalogAsync(student, Ct));

        await Admin.SetDisabledAsync(_admin, AccountRole.Student, MertId, true, Ct);

        var signIn = await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInStudentAsync("Mert Deneme", StudentPassword, Ct));
        var wrong = await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInStudentAsync("Selin Deneme", "yanlis", Ct));
        Assert.Equal(wrong.Message, signIn.Message);
        await Assert.ThrowsAsync<SessionExpiredException>(() => courses.GetCatalogAsync(student, Ct));
        await Assert.ThrowsAsync<SessionExpiredException>(() => _db.Get<IEnrollmentService>().EnrollAsync(student, DenizCourseId, Ct));
        await using (var context = _db.CreateContext())
        {
            Assert.Equal(0, await context.Enrollments.CountAsync(Ct));
            Assert.Equal(0, (await context.Students.SingleAsync(s => s.Id == MertId, Ct)).FailedLoginCount);
        }

        await Admin.SetDisabledAsync(_admin, AccountRole.Student, MertId, false, Ct);
        var fresh = await Auth.SignInStudentAsync("Mert Deneme", StudentPassword, Ct);
        Assert.NotEmpty(await courses.GetCatalogAsync(fresh, Ct));
        await Assert.ThrowsAsync<SessionExpiredException>(() => courses.GetCatalogAsync(student, Ct));
    }

    /// <summary>
    /// The only administrator who can sign in can neither be disabled nor demoted, also not by
    /// themselves. With a second one, either may go; a disabled or passwordless administrator does not
    /// count as the remaining one.
    /// </summary>
    [Fact]
    public async Task LastActiveAdmin_StaysWhateverIsTried()
    {
        var adminId = _admin.UserId;
        var disable = await Assert.ThrowsAsync<BusinessRuleException>(() => Admin.SetDisabledAsync(_admin, AccountRole.Admin, adminId, true, Ct));
        var demote = await Assert.ThrowsAsync<BusinessRuleException>(() => Admin.DemoteAdminAsync(_admin, adminId, Ct));
        Assert.Equal(AdministrationService.LastActiveAdmin, disable.Message);
        Assert.Equal(AdministrationService.LastActiveAdmin, demote.Message);

        // A second administrator without a password, or a disabled one, is not a remaining administrator.
        var passwordless = await AddAdminAsync("Şifresiz Yönetici", withPassword: false);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Admin.DemoteAdminAsync(_admin, adminId, Ct));
        var second = await AddAdminAsync("İkinci Yönetici");
        await Admin.SetDisabledAsync(_admin, AccountRole.Admin, second, true, Ct);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Admin.DemoteAdminAsync(_admin, adminId, Ct));

        // An active second administrator lets the first go; the first one's session ends with it.
        await Admin.SetDisabledAsync(_admin, AccountRole.Admin, second, false, Ct);
        await Admin.DemoteAdminAsync(_admin, adminId, Ct);
        await Assert.ThrowsAsync<SessionExpiredException>(() => Admin.SearchAccountsAsync(_admin, new AccountSearch(), Ct));
        var asTeacher = await Auth.SignInTeacherAsync(AdminName, AdminPassword, Ct);
        Assert.True(asTeacher.IsTeacher);

        var other = await Auth.SignInTeacherAsync("İkinci Yönetici", AdminPassword, Ct);
        var self = await Assert.ThrowsAsync<BusinessRuleException>(() => Admin.SetDisabledAsync(other, AccountRole.Admin, second, true, Ct));
        Assert.Equal(AdministrationService.LastActiveAdmin, self.Message);
        await Admin.SetDisabledAsync(other, AccountRole.Admin, passwordless, true, Ct); // not active: may go
        await Assert.ThrowsAsync<BusinessRuleException>(() => Admin.DemoteAdminAsync(other, asTeacher.UserId, Ct)); // a teacher now
    }

    /// <summary>
    /// The role is checked against the database at the time of every operation, not taken from the
    /// session object: an administrator demoted by another one keeps an open window but can manage no
    /// account with it, and gets the one "session ended" answer.
    /// </summary>
    [Fact]
    public async Task DemotedAdminSession_CannotManageUsers()
    {
        var second = await AddAdminAsync("İkinci Yönetici");
        var other = await Auth.SignInTeacherAsync("İkinci Yönetici", AdminPassword, Ct);

        await Admin.DemoteAdminAsync(other, _admin.UserId, Ct);

        var errors = new Exception[]
        {
            await Assert.ThrowsAsync<SessionExpiredException>(() => Admin.SearchAccountsAsync(_admin, new AccountSearch(), Ct)),
            await Assert.ThrowsAsync<SessionExpiredException>(() => Admin.CreateTeacherAsync(_admin, "Sızan Öğretmen", "Fizik", Ct)),
            await Assert.ThrowsAsync<SessionExpiredException>(() => Admin.SetDisabledAsync(_admin, AccountRole.Admin, second, true, Ct)),
            await Assert.ThrowsAsync<SessionExpiredException>(() => Admin.DemoteAdminAsync(_admin, second, Ct)),
        };
        Assert.All(errors, e => Assert.Equal(SessionExpiredException.DefaultMessage, e.Message));
        await using var context = _db.CreateContext();
        Assert.False(await context.Teachers.AnyAsync(t => t.FullName == "Sızan Öğretmen", Ct));
        Assert.False(await context.Teachers.AnyAsync(t => t.Id == second && (t.Disabled || t.Role != AccountRole.Admin), Ct));
    }

    /// <summary>
    /// A teacher disabled while signed in can no longer create, edit or delete courses, add notes or
    /// issue codes with the open session; nothing is written.
    /// </summary>
    [Fact]
    public async Task DeactivatedTeacherSession_CannotEditCourses()
    {
        var teacher = await Auth.SignInTeacherAsync("Deniz Örnek", TeacherPassword, Ct);
        var courses = _db.Get<ICourseService>();
        await Admin.SetDisabledAsync(_admin, AccountRole.Teacher, DenizId, true, Ct);

        var errors = new Exception[]
        {
            await Assert.ThrowsAsync<SessionExpiredException>(() => courses.CreateAsync(teacher, "Yeni Ders", "Konu", null, Ct)),
            await Assert.ThrowsAsync<SessionExpiredException>(() => courses.UpdateAsync(teacher, DenizCourseId, "Değişti", "Konu", 5, Ct)),
            await Assert.ThrowsAsync<SessionExpiredException>(() => courses.DeleteAsync(teacher, DenizCourseId, 0, Ct)),
            await Assert.ThrowsAsync<SessionExpiredException>(() => courses.AddNoteAsync(teacher, DenizCourseId, "Not", Ct)),
            await Assert.ThrowsAsync<SessionExpiredException>(() => _db.Get<IRecoveryService>().IssueStudentCodeAsync(teacher, "Mert Deneme", Ct)),
        };
        Assert.All(errors, e => Assert.Equal(SessionExpiredException.DefaultMessage, e.Message));
        await using var context = _db.CreateContext();
        var course = await context.Courses.SingleAsync(c => c.Id == DenizCourseId, Ct);
        Assert.Equal(("Temel Programlama", (int?)null, (DateTime?)null), (course.Name, course.Capacity, course.DeletedUtc));
        Assert.Equal(2, await context.Courses.CountAsync(Ct));
        Assert.Equal(0, await context.Notes.CountAsync(Ct));
    }

    /// <summary>The role and the id must belong together: a teacher id given as an administrator, or the reverse, is a missing account.</summary>
    [Fact]
    public async Task SetDisabled_WrongRoleForTheId_IsAMissingAccount()
    {
        var asAdmin = await Assert.ThrowsAsync<BusinessRuleException>(() => Admin.SetDisabledAsync(_admin, AccountRole.Admin, DenizId, true, Ct));
        var asTeacher = await Assert.ThrowsAsync<BusinessRuleException>(() => Admin.SetDisabledAsync(_admin, AccountRole.Teacher, _admin.UserId, true, Ct));
        var missing = await Assert.ThrowsAsync<BusinessRuleException>(() => Admin.SetDisabledAsync(_admin, AccountRole.Student, 999, true, Ct));

        Assert.All(new[] { asAdmin, asTeacher, missing }, e => Assert.Equal(AdministrationService.AccountNotFound, e.Message));
        await using var context = _db.CreateContext();
        Assert.False(await context.Teachers.AnyAsync(t => t.Disabled, Ct));
    }

    [Fact]
    public async Task Search_ListsBothTablesLiterallyAndPagesStably()
    {
        await Admin.CreateStudentAsync(_admin, "Yüzde Öğrenci", "yuzde%100@example.com", Ct);

        var all = await Admin.SearchAccountsAsync(_admin, new AccountSearch(), Ct);
        var students = await Admin.SearchAccountsAsync(_admin, new AccountSearch(Role: AccountRole.Student), Ct);
        var admins = await Admin.SearchAccountsAsync(_admin, new AccountSearch(Role: AccountRole.Admin), Ct);
        var percent = await Admin.SearchAccountsAsync(_admin, new AccountSearch("%"), Ct);
        var byMail = await Admin.SearchAccountsAsync(_admin, new AccountSearch("selin@"), Ct);
        var first = await Admin.SearchAccountsAsync(_admin, new AccountSearch(PageSize: 2), Ct);
        var second = await Admin.SearchAccountsAsync(_admin, new AccountSearch(PageIndex: 1, PageSize: 2), Ct);

        Assert.Equal(6, all.TotalCount);
        Assert.Equal(["Ayla Yönetici", "Deniz Örnek", "Ece Taslak", "Mert Deneme", "Selin Deneme", "Yüzde Öğrenci"], all.Items.Select(a => a.FullName));
        Assert.Equal(3, students.TotalCount);
        Assert.Equal([AdminName], admins.Items.Select(a => a.FullName));
        Assert.Equal(["Yüzde Öğrenci"], percent.Items.Select(a => a.FullName));
        Assert.False(percent.Items[0].HasPassword);
        Assert.Equal(["Selin Deneme"], byMail.Items.Select(a => a.FullName));
        Assert.Equal(all.Items.Take(4), first.Items.Concat(second.Items));
    }

    private async Task<int> AddAdminAsync(string name, bool withPassword = true)
    {
        await using var context = _db.CreateContext();
        var admin = new Teacher
        {
            FullName = name,
            Specialty = "Yönetim",
            Role = AccountRole.Admin,
            PasswordHash = withPassword ? _db.Get<Business.Security.IPasswordHasher>().Hash(AdminPassword) : string.Empty,
            PasswordFormat = withPassword ? PasswordFormat.Pbkdf2 : PasswordFormat.None,
        };
        context.Teachers.Add(admin);
        await context.SaveChangesAsync(Ct);
        return admin.Id;
    }
}
