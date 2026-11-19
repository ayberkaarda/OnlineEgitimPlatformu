using Microsoft.EntityFrameworkCore;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Domain.Entities;
using static OnlineEgitim.Data.Tests.TestServices;

namespace OnlineEgitim.Data.Tests;

/// <summary>
/// Changing one's own password and setting a forgotten one with a code, on SQLite in-memory: every
/// session ends, codes work once and for 24 hours, wrong codes lock like wrong passwords, and only the
/// course's teacher or an administrator issues a code.
/// </summary>
public sealed class PasswordResetTests : IAsyncLifetime
{
    private const string AdminPassword = "Gizli.Yonetici1";

    private readonly SqliteDatabase _db = new();
    private UserSession _admin = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IAuthService Auth => _db.Get<IAuthService>();

    private IRecoveryService Recovery => _db.Get<IRecoveryService>();

    private IAdministrationService Admin => _db.Get<IAdministrationService>();

    public async ValueTask InitializeAsync()
    {
        await _db.InitializeAsync();
        await using (var context = _db.CreateContext())
        {
            context.Teachers.Add(new Teacher { FullName = "Ayla Yönetici", Specialty = "Yönetim", Role = AccountRole.Admin, PasswordHash = _db.Get<IPasswordHasher>().Hash(AdminPassword) });
            await context.SaveChangesAsync(Ct);
        }

        _admin = await Auth.SignInTeacherAsync("Ayla Yönetici", AdminPassword, Ct);
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task ChangePassword_EndsEverySessionAndReplacesThePassword()
    {
        var here = await Auth.SignInStudentAsync("Mert Deneme", StudentPassword, Ct);
        var elsewhere = await Auth.SignInStudentAsync("Mert Deneme", StudentPassword, Ct);

        await Auth.ChangePasswordAsync(here, StudentPassword, "Yeni.Sifre1", "Yeni.Sifre1", Ct);

        await Assert.ThrowsAsync<SessionExpiredException>(() => _db.Get<ICourseService>().GetCatalogAsync(here, Ct));
        await Assert.ThrowsAsync<SessionExpiredException>(() => _db.Get<IEnrollmentService>().GetMyCoursesAsync(elsewhere, Ct));
        await Assert.ThrowsAsync<SessionExpiredException>(() => Auth.ChangePasswordAsync(elsewhere, StudentPassword, "Baska.Sifre1", "Baska.Sifre1", Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInStudentAsync("Mert Deneme", StudentPassword, Ct));
        var fresh = await Auth.SignInStudentAsync("Mert Deneme", "Yeni.Sifre1", Ct);
        Assert.NotEmpty(await _db.Get<ICourseService>().GetCatalogAsync(fresh, Ct));
    }

    /// <summary>Five wrong current passwords lock the account, for the change and for the sign-in alike.</summary>
    [Fact]
    public async Task ChangePassword_WrongCurrentPasswordsLockTheAccount()
    {
        var session = await Auth.SignInTeacherAsync("Deniz Örnek", TeacherPassword, Ct);
        for (var i = 0; i < LoginPolicy.MaxFailedAttempts; i++)
        {
            var wrong = await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.ChangePasswordAsync(session, "Yanlis.Sifre1", "Yeni.Sifre1", "Yeni.Sifre1", Ct));
            Assert.Equal(AuthService.PasswordChangeFailed, wrong.Message);
        }

        var locked = await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.ChangePasswordAsync(session, TeacherPassword, "Yeni.Sifre1", "Yeni.Sifre1", Ct));
        Assert.Equal(AuthService.PasswordChangeFailed, locked.Message);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInTeacherAsync("Deniz Örnek", TeacherPassword, Ct));

        _db.Clock.Advance(LoginPolicy.LockoutDuration);
        await Auth.ChangePasswordAsync(session, TeacherPassword, "Yeni.Sifre1", "Yeni.Sifre1", Ct);
        Assert.True((await Auth.SignInTeacherAsync("Deniz Örnek", "Yeni.Sifre1", Ct)).IsTeacher);
    }

    /// <summary>
    /// A student who forgot the password gets a code from an administrator. The old password works until
    /// the code is used; then the new one replaces it, every open session ends and the code is spent.
    /// </summary>
    [Fact]
    public async Task ForgottenPassword_AdminCodeSetsANewOneOnce()
    {
        var open = await Auth.SignInStudentAsync("Mert Deneme", StudentPassword, Ct);
        var code = await Admin.IssueResetCodeAsync(_admin, AccountRole.Student, MertId, Ct);
        Assert.Equal(open.UserId, (await Auth.SignInStudentAsync("Mert Deneme", StudentPassword, Ct)).UserId);

        var session = await Recovery.RecoverStudentAsync("Mert Deneme", code.Code, "Yeni.Sifre1", "Yeni.Sifre1", Ct);

        Assert.Equal(MertId, session.UserId);
        Assert.NotEmpty(await _db.Get<ICourseService>().GetCatalogAsync(session, Ct));
        await Assert.ThrowsAsync<SessionExpiredException>(() => _db.Get<ICourseService>().GetCatalogAsync(open, Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInStudentAsync("Mert Deneme", StudentPassword, Ct));
        var reused = await Assert.ThrowsAsync<BusinessRuleException>(() => Recovery.RecoverStudentAsync("Mert Deneme", code.Code, "Baska.Sifre1", "Baska.Sifre1", Ct));
        Assert.Equal(RecoveryService.StudentRecoveryFailed, reused.Message);
        Assert.True((await Auth.SignInStudentAsync("Mert Deneme", "Yeni.Sifre1", Ct)).IsStudent);
    }

    /// <summary>A code works until 24 hours after it was issued and not a second longer.</summary>
    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    public async Task ResetCode_ExpiresAfterTwentyFourHours(int secondsFromExpiry, bool works)
    {
        var code = await Admin.IssueResetCodeAsync(_admin, AccountRole.Teacher, DenizId, Ct);
        _db.Clock.Advance(RecoveryPolicy.CodeLifetime + TimeSpan.FromSeconds(secondsFromExpiry));

        var attempt = Recovery.RecoverTeacherAsync(code.Code, "Deniz Örnek", "Yeni.Sifre1", "Yeni.Sifre1", Ct);

        if (works)
        {
            Assert.True((await attempt).IsTeacher);
        }
        else
        {
            Assert.Equal(RecoveryService.StudentRecoveryFailed, (await Assert.ThrowsAsync<BusinessRuleException>(() => attempt)).Message);
            Assert.True((await Auth.SignInTeacherAsync("Deniz Örnek", TeacherPassword, Ct)).IsTeacher);
        }
    }

    /// <summary>Wrong codes count like wrong passwords: after five the account is locked and even the right code waits for the lock to end.</summary>
    [Fact]
    public async Task WrongCodes_LockTheAccountLikeWrongPasswords()
    {
        var code = await Admin.IssueResetCodeAsync(_admin, AccountRole.Student, SelinId, Ct);
        for (var i = 0; i < LoginPolicy.MaxFailedAttempts; i++)
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => Recovery.RecoverStudentAsync("Selin Deneme", "ZZZZZ-ZZZZZ", "Yeni.Sifre1", "Yeni.Sifre1", Ct));
        }

        var locked = await Assert.ThrowsAsync<BusinessRuleException>(() => Recovery.RecoverStudentAsync("Selin Deneme", code.Code, "Yeni.Sifre1", "Yeni.Sifre1", Ct));
        Assert.Equal(RecoveryService.StudentRecoveryFailed, locked.Message);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Auth.SignInStudentAsync("Selin Deneme", StudentPassword, Ct));

        _db.Clock.Advance(LoginPolicy.LockoutDuration);
        Assert.True((await Recovery.RecoverStudentAsync("Selin Deneme", code.Code, "Yeni.Sifre1", "Yeni.Sifre1", Ct)).IsStudent);
    }

    /// <summary>
    /// IDOR: a teacher issues codes only for students of their own courses. Another teacher's student, a
    /// student of no course and an unknown name get one answer and nothing changes; a disabled account's
    /// code is refused like a wrong one.
    /// </summary>
    [Fact]
    public async Task TeacherCode_OnlyForOwnStudents_AndNeverForADisabledAccount()
    {
        await _db.Get<IEnrollmentService>().EnrollAsync(Student(MertId), DenizCourseId, Ct);
        await _db.Get<IEnrollmentService>().EnrollAsync(Student(SelinId), EceCourseId, Ct);
        var deniz = Teacher(DenizId);

        var own = await Recovery.IssueStudentCodeAsync(deniz, "Mert Deneme", Ct);
        var errors = new[]
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => Recovery.IssueStudentCodeAsync(deniz, "Selin Deneme", Ct)),
            await Assert.ThrowsAsync<BusinessRuleException>(() => Recovery.IssueStudentCodeAsync(deniz, "Olmayan Öğrenci", Ct)),
        };
        Assert.All(errors, e => Assert.Equal(RecoveryService.StudentNotFound, e.Message));
        await using (var context = _db.CreateContext())
        {
            Assert.Null((await context.Students.SingleAsync(s => s.Id == SelinId, Ct)).RecoveryCodeHash);
        }

        await Admin.SetDisabledAsync(_admin, AccountRole.Student, MertId, true, Ct);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Recovery.RecoverStudentAsync("Mert Deneme", own.Code, "Yeni.Sifre1", "Yeni.Sifre1", Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Recovery.IssueStudentCodeAsync(deniz, "Mert Deneme", Ct));
        await Admin.SetDisabledAsync(_admin, AccountRole.Student, MertId, false, Ct);
        Assert.True((await Recovery.RecoverStudentAsync("Mert Deneme", own.Code, "Yeni.Sifre1", "Yeni.Sifre1", Ct)).IsStudent);
    }

    /// <summary>
    /// The code was verified, and right before the password is written an administrator issues a new
    /// code: the final write sees that the verified code is gone and sets nothing; the new code works.
    /// </summary>
    [Fact]
    public async Task ResetCode_ReplacedBetweenCheckAndWrite_WritesNothing()
    {
        var hook = new CommandHook("UPDATE", "Ogrenci", "KurtarmaKodu", "SifreBicimi");
        await using var db = new SqliteDatabase(configureContext: b => b.AddInterceptors(hook));
        await db.InitializeAsync();
        await using (var context = db.CreateContext())
        {
            context.Teachers.Add(new Teacher { FullName = "Ayla Yönetici", Specialty = "Yönetim", Role = AccountRole.Admin, PasswordHash = db.Get<IPasswordHasher>().Hash(AdminPassword) });
            await context.SaveChangesAsync(Ct);
        }

        var admin = await db.Get<IAuthService>().SignInTeacherAsync("Ayla Yönetici", AdminPassword, Ct);
        var administration = db.Get<IAdministrationService>();
        var first = await administration.IssueResetCodeAsync(admin, AccountRole.Student, MertId, Ct);
        RecoveryCode? second = null;
        hook.Arm(async () => second = await administration.IssueResetCodeAsync(admin, AccountRole.Student, MertId, Ct));

        var refused = await Assert.ThrowsAsync<BusinessRuleException>(
            () => db.Get<IRecoveryService>().RecoverStudentAsync("Mert Deneme", first.Code, "Yeni.Sifre1", "Yeni.Sifre1", Ct));

        Assert.True(hook.Fired);
        Assert.Equal(RecoveryService.StudentRecoveryFailed, refused.Message);
        Assert.True((await db.Get<IAuthService>().SignInStudentAsync("Mert Deneme", StudentPassword, Ct)).IsStudent);
        Assert.True((await db.Get<IRecoveryService>().RecoverStudentAsync("Mert Deneme", second!.Code, "Yeni.Sifre1", "Yeni.Sifre1", Ct)).IsStudent);
    }

    /// <summary>An administrator's own reset code makes the session an administrator's again; a code for another role's id is a missing account.</summary>
    [Fact]
    public async Task AdminResetCode_KeepsTheRoleAndChecksIt()
    {
        var code = await Admin.IssueResetCodeAsync(_admin, AccountRole.Admin, _admin.UserId, Ct);
        var wrong = await Assert.ThrowsAsync<BusinessRuleException>(() => Admin.IssueResetCodeAsync(_admin, AccountRole.Teacher, _admin.UserId, Ct));
        Assert.Equal(AdministrationService.AccountNotFound, wrong.Message);

        var session = await Recovery.RecoverTeacherAsync(code.Code, "Ayla Yönetici", "Yeni.Yonetici1", "Yeni.Yonetici1", Ct);

        Assert.True(session.IsAdmin);
        await Assert.ThrowsAsync<SessionExpiredException>(() => Admin.SearchAccountsAsync(_admin, new AccountSearch(), Ct));
        Assert.Equal(5, (await Admin.SearchAccountsAsync(session, new AccountSearch(), Ct)).TotalCount);
    }
}
