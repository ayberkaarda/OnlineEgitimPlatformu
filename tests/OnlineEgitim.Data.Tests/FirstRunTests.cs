using Microsoft.EntityFrameworkCore;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Data.Persistence;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Data.Tests;

/// <summary>
/// A new database has no account and no known password: the first teacher is created by the setup,
/// and the demonstration data exists only when it is asked for.
/// </summary>
public sealed class FirstRunTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Initialize_DefaultConfiguration_DoesNotCreateDemoCredentials()
    {
        await using var db = new SqliteDatabase(seed: false);
        await db.InitializeAsync();

        await using (var context = db.CreateContext())
        {
            Assert.Equal(0, await context.Teachers.CountAsync(Ct));
            Assert.Equal(0, await context.Students.CountAsync(Ct));
            Assert.Equal(0, await context.Courses.CountAsync(Ct));
        }

        var auth = db.Get<IAuthService>();
        await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInTeacherAsync("Deniz Örnek", "Demo.Ogretmen1", Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInStudentAsync("Mert Deneme", "Demo.Ogrenci1", Ct));
        Assert.True(await db.Get<ISetupService>().IsSetupRequiredAsync(Ct));
    }

    [Fact]
    public async Task Setup_EmptyDatabase_CreatesTheFirstTeacherWithoutAKeyOnce()
    {
        await using var db = new SqliteDatabase(seed: false);
        await db.InitializeAsync();
        var setup = db.Get<ISetupService>();
        Assert.Equal(SetupStage.EmptyDatabase, await setup.GetStageAsync(Ct));

        var mismatch = await Assert.ThrowsAsync<BusinessRuleException>(
            () => setup.CreateFirstTeacherAsync(null, "Ada Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre2", Ct));
        var weak = await Assert.ThrowsAsync<BusinessRuleException>(
            () => setup.CreateFirstTeacherAsync(null, "Ada Kurucu", "Matematik", "kisa", "kisa", Ct));
        var session = await setup.CreateFirstTeacherAsync(null, " Ada Kurucu ", " Matematik ", "Gizli.Sifre1", "Gizli.Sifre1", Ct);

        Assert.Equal(SetupService.PasswordsDoNotMatch, mismatch.Message);
        Assert.Equal(AuthService.PasswordInvalid, weak.Message);
        Assert.True(session.IsTeacher);
        Assert.False(await setup.IsSetupRequiredAsync(Ct));
        Assert.Equal(SetupStage.Completed, await setup.GetStageAsync(Ct));
        Assert.Equal(session.UserId, (await db.Get<IAuthService>().SignInTeacherAsync("Ada Kurucu", "Gizli.Sifre1", Ct)).UserId);
        var second = await Assert.ThrowsAsync<BusinessRuleException>(
            () => setup.CreateFirstTeacherAsync(null, "İkinci Kişi", "Fizik", "Gizli.Sifre1", "Gizli.Sifre1", Ct));
        Assert.Equal(SetupService.AlreadyDone, second.Message);
    }

    /// <summary>
    /// A database with students but no teacher keeps its normal sign-in. Having no teacher grants
    /// nobody the right to become one: the first teacher needs the database owner's setup key, which
    /// is checked in the write itself and works once.
    /// </summary>
    [Fact]
    public async Task Setup_ExistingStudents_RequiresBootstrapAuthority()
    {
        await using var db = new SqliteDatabase(seed: false);
        await db.InitializeAsync();
        await AddStudentAsync(db, "Mevcut Öğrenci");
        var setup = db.Get<ISetupService>();
        Assert.False(await setup.IsSetupRequiredAsync(Ct));
        Assert.Equal(SetupStage.FirstTeacherNeedsKey, await setup.GetStageAsync(Ct));

        // No key exists yet: nothing works, not even a key of the right form.
        var none = await Assert.ThrowsAsync<BusinessRuleException>(
            () => setup.CreateFirstTeacherAsync(null, "Yetkisiz Kişi", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", Ct));
        Assert.Equal(SetupService.SetupKeyRequired, none.Message);
        var invented = await Assert.ThrowsAsync<BusinessRuleException>(
            () => setup.CreateFirstTeacherAsync(Convert.ToHexString(new byte[16]), "Yetkisiz Kişi", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", Ct));
        Assert.Equal(SetupService.SetupKeyInvalid, invented.Message);

        var key = await AddKeyAsync(db);
        foreach (var wrong in new[] { "YANLIS-ANAHTAR-0000", key + "0", key[..^1], new string('-', 32), key[..^1] + "G" })
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(
                () => setup.CreateFirstTeacherAsync(wrong, "Yetkisiz Kişi", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", Ct));
            Assert.Equal(SetupService.SetupKeyInvalid, error.Message);
        }

        await using (var context = db.CreateContext())
        {
            Assert.Equal(0, await context.Teachers.CountAsync(Ct));
            Assert.Equal(0, await TestServices.ConsumedSetupKeysAsync(context, Ct));
        }

        // The founder, with the key; typed in lower case and with other separators.
        var founder = await setup.CreateFirstTeacherAsync(
            " " + string.Join(' ', key.ToLowerInvariant().Chunk(8).Select(c => new string(c))) + " ", "Ada Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", Ct);
        Assert.True(founder.IsTeacher);
        Assert.Equal(SetupStage.Completed, await setup.GetStageAsync(Ct));
        await using (var context = db.CreateContext())
        {
            Assert.Equal(1, await context.Teachers.CountAsync(Ct));
            Assert.Equal(1, await context.Students.CountAsync(Ct));
            Assert.Equal(1, await TestServices.ConsumedSetupKeysAsync(context, Ct));
        }
    }

    /// <summary>
    /// Wrong keys are never counted against the stored key: however many are tried, the founder's key
    /// still works, and it works until it expires.
    /// </summary>
    [Fact]
    public async Task SetupKey_WrongAttemptsNeverRevokeTheKey()
    {
        await using var db = new SqliteDatabase(seed: false);
        await db.InitializeAsync();
        await AddStudentAsync(db, "Mevcut Öğrenci");
        var key = await AddKeyAsync(db);
        var setup = db.Get<ISetupService>();

        for (var i = 0; i < 25; i++)
        {
            await Assert.ThrowsAsync<BusinessRuleException>(
                () => setup.CreateFirstTeacherAsync(Convert.ToHexString(BitConverter.GetBytes((long)i)) + "0000000000000000", "Yetkisiz Kişi", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", Ct));
        }

        await using (var context = db.CreateContext())
        {
            var stored = await context.SetupKeys.SingleAsync(Ct);
            Assert.False(stored.Consumed);
        }

        Assert.True((await setup.CreateFirstTeacherAsync(key, "Ada Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", Ct)).IsTeacher);
    }

    [Fact]
    public async Task SetupKey_AfterTwentyFourHours_IsRefused()
    {
        await using var db = new SqliteDatabase(seed: false);
        await db.InitializeAsync();
        await AddStudentAsync(db, "Mevcut Öğrenci");
        var key = await AddKeyAsync(db);
        db.Clock.Advance(TimeSpan.FromHours(24));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => db.Get<ISetupService>().CreateFirstTeacherAsync(key, "Ada Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", Ct));

        Assert.Equal(SetupService.SetupKeyInvalid, error.Message);
        await using var context = db.CreateContext();
        Assert.Equal(0, await context.Teachers.CountAsync(Ct));
    }

    /// <summary>
    /// The key is used up, then the teacher's insert fails, and in a second attempt the commit fails.
    /// Neither may spend the key: the same key works for the next attempt, once.
    /// </summary>
    [Fact]
    public async Task Setup_FailedWriteDoesNotSpendAuthority()
    {
        var insertFailure = new CommandFailure("INSERT", "Egitmenler");
        var commitFailure = new CommitFailure();
        await using var db = new SqliteDatabase(seed: false, configureContext: b => b.AddInterceptors(insertFailure, commitFailure));
        await db.InitializeAsync();
        await AddStudentAsync(db, "Mevcut Öğrenci");
        var key = await AddKeyAsync(db);
        var setup = db.Get<ISetupService>();

        insertFailure.Arm();
        await Assert.ThrowsAnyAsync<Exception>(
            () => setup.CreateFirstTeacherAsync(key, "Ada Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", Ct));
        commitFailure.Arm();
        await Assert.ThrowsAsync<InjectedFailureException>(
            () => setup.CreateFirstTeacherAsync(key, "Ada Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", Ct));

        Assert.True(insertFailure.Fired);
        Assert.True(commitFailure.Fired);
        await using (var context = db.CreateContext())
        {
            Assert.Equal(0, await context.Teachers.CountAsync(Ct));
            Assert.Equal(0, await TestServices.ConsumedSetupKeysAsync(context, Ct));
        }

        Assert.True((await setup.CreateFirstTeacherAsync(key, "Ada Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", Ct)).IsTeacher);
        await using (var context = db.CreateContext())
        {
            Assert.Equal(1, await TestServices.ConsumedSetupKeysAsync(context, Ct));
        }

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => setup.CreateFirstTeacherAsync(key, "İkinci Kişi", "Fizik", "Gizli.Sifre1", "Gizli.Sifre1", Ct));
    }

    /// <summary>
    /// The teacher recovery equivalent: a failed password write or a failed commit leaves the password
    /// unset and the key unused.
    /// </summary>
    [Fact]
    public async Task TeacherRecovery_FailedWriteDoesNotSpendAuthority()
    {
        var updateFailure = new CommandFailure("UPDATE", "Egitmenler", "SifreBicimi");
        var failure = new CommitFailure();
        await using var db = new SqliteDatabase(seed: false, configureContext: b => b.AddInterceptors(updateFailure, failure));
        await db.InitializeAsync();
        await using (var context = db.CreateContext())
        {
            context.Teachers.AddRange(
                new Domain.Entities.Teacher { FullName = "Şifresiz Öğretmen", Specialty = "Fizik", PasswordFormat = Domain.Entities.PasswordFormat.None },
                new Domain.Entities.Teacher { FullName = "Diğer Şifresiz", Specialty = "Kimya", PasswordFormat = Domain.Entities.PasswordFormat.None });
            await context.SaveChangesAsync(Ct);
        }

        var key = await AddKeyAsync(db);
        var recovery = db.Get<IRecoveryService>();
        Assert.Equal(SetupStage.TeacherPasswordNeedsKey, await db.Get<ISetupService>().GetStageAsync(Ct));

        updateFailure.Arm();
        await Assert.ThrowsAsync<InjectedFailureException>(
            () => recovery.RecoverTeacherAsync(key, "Şifresiz Öğretmen", "Yeni.Sifre1", "Yeni.Sifre1", Ct));
        failure.Arm();
        await Assert.ThrowsAsync<InjectedFailureException>(
            () => recovery.RecoverTeacherAsync(key, "Şifresiz Öğretmen", "Yeni.Sifre1", "Yeni.Sifre1", Ct));
        Assert.True(updateFailure.Fired);
        Assert.True(failure.Fired);
        await using (var context = db.CreateContext())
        {
            Assert.Equal(2, await context.Teachers.CountAsync(t => t.PasswordFormat == Domain.Entities.PasswordFormat.None, Ct));
            Assert.Equal(0, await TestServices.ConsumedSetupKeysAsync(context, Ct));
        }

        // A matching key with a name that has no passwordless account is not spent either.
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => recovery.RecoverTeacherAsync(key, "Olmayan Öğretmen", "Yeni.Sifre1", "Yeni.Sifre1", Ct));

        var teacher = await recovery.RecoverTeacherAsync(key, "Şifresiz Öğretmen", "Yeni.Sifre1", "Yeni.Sifre1", Ct);
        Assert.True(teacher.IsTeacher);

        // Now a teacher has a password: no key is accepted, also not a fresh one.
        var fresh = await AddKeyAsync(db);
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(
            () => recovery.RecoverTeacherAsync(fresh, "Diğer Şifresiz", "Yeni.Sifre1", "Yeni.Sifre1", Ct));
        Assert.Equal(SetupService.SetupKeyNotAllowed, refused.Message);
        await using (var context = db.CreateContext())
        {
            Assert.Equal(1, await TestServices.ConsumedSetupKeysAsync(context, Ct));
        }
    }

    /// <summary>
    /// A passwordless student enrolled in no course has no teacher who could issue a code; while no
    /// teacher can sign in with a password the founder's key sets its password instead, once.
    /// </summary>
    [Fact]
    public async Task UnenrolledStudent_RecoveredOnlyWithTheSetupKey()
    {
        await using var db = new SqliteDatabase(seed: false);
        await db.InitializeAsync();
        await using (var context = db.CreateContext())
        {
            context.Students.Add(new Domain.Entities.Student { FullName = "Derssiz Öğrenci", Email = "derssiz@example.com", PasswordFormat = Domain.Entities.PasswordFormat.None });
            await context.SaveChangesAsync(Ct);
        }

        var key = await AddKeyAsync(db);
        var recovery = db.Get<IRecoveryService>();

        var student = await recovery.RecoverStudentAsync("Derssiz Öğrenci", key, "Yeni.Sifre1", "Yeni.Sifre1", Ct);

        Assert.True(student.IsStudent);
        Assert.Equal(student.UserId, (await db.Get<IAuthService>().SignInStudentAsync("Derssiz Öğrenci", "Yeni.Sifre1", Ct)).UserId);
        await Assert.ThrowsAsync<BusinessRuleException>(() => recovery.RecoverStudentAsync("Derssiz Öğrenci", key, "Baska.Sifre1", "Baska.Sifre1", Ct));
    }

    private static async Task AddStudentAsync(SqliteDatabase db, string name)
    {
        await using var context = db.CreateContext();
        context.Students.Add(new Domain.Entities.Student { FullName = name, Email = Guid.NewGuid().ToString("N") + "@example.com", PasswordHash = "-" });
        await context.SaveChangesAsync(Ct);
    }

    private static async Task<string> AddKeyAsync(SqliteDatabase db)
    {
        await using var context = db.CreateContext();
        return await TestServices.AddSetupKeyAsync(context, db.Clock.GetUtcNow().UtcDateTime, Ct);
    }

    [Fact]
    public async Task Initialize_DemoRequested_AddsDemoDataToAnEmptyDatabaseOnly()
    {
        await using var db = new SqliteDatabase(seed: false, new DataAccessOptions { SeedDemoData = true });
        await db.InitializeAsync();
        await db.Get<IDatabaseInitializer>().InitializeAsync(Ct); // a second start adds nothing

        await using (var context = db.CreateContext())
        {
            Assert.Equal(2, await context.Teachers.CountAsync(Ct));
            Assert.Equal(2, await context.Students.CountAsync(Ct));
            Assert.Equal(3, await context.Courses.CountAsync(Ct));
            Assert.All(await context.Students.Select(s => s.Email).ToListAsync(Ct), e => Assert.EndsWith("@example.com", e, StringComparison.Ordinal));
        }

        var teacher = await db.Get<IAuthService>().SignInTeacherAsync("Deniz Örnek", "Demo.Ogretmen1", Ct);
        Assert.Equal(2, (await db.Get<ICourseService>().GetTeachingAsync(teacher, Ct)).Count);
    }

    /// <summary>
    /// The demo accounts are written, then writing the courses fails: nothing of the demo data may stay,
    /// so the next start finds an empty database and writes all of it.
    /// </summary>
    [Fact]
    public async Task DemoSeed_FailureAfterAccounts_RollsBackEverything()
    {
        var failure = SaveHook.FailOnce(e => e is Domain.Entities.Course);
        await using var db = new SqliteDatabase(seed: false, new DataAccessOptions { SeedDemoData = true }, b => b.AddInterceptors(failure));

        var error = await Assert.ThrowsAsync<DatabaseInitializationException>(() => db.InitializeAsync().AsTask());

        Assert.IsType<InjectedFailureException>(error.InnerException);
        Assert.True(failure.Fired);
        await using (var context = db.CreateContext())
        {
            Assert.Equal(0, await context.Teachers.CountAsync(Ct));
            Assert.Equal(0, await context.Students.CountAsync(Ct));
            Assert.Equal(0, await context.Courses.CountAsync(Ct));
            Assert.Equal(0, await context.Notes.CountAsync(Ct));
            Assert.Equal(0, await context.Enrollments.CountAsync(Ct));
        }

        Assert.True(await db.Get<ISetupService>().IsSetupRequiredAsync(Ct));

        // The next start completes the demo data.
        await db.Get<IDatabaseInitializer>().InitializeAsync(Ct);
        await using (var context = db.CreateContext())
        {
            Assert.Equal(2, await context.Teachers.CountAsync(Ct));
            Assert.Equal(2, await context.Students.CountAsync(Ct));
            Assert.Equal(3, await context.Courses.CountAsync(Ct));
            Assert.Equal(2, await context.Notes.CountAsync(Ct));
            Assert.Equal(1, await context.Enrollments.CountAsync(Ct));
        }
    }

    [Fact]
    public async Task Initialize_DemoRequested_LeavesADatabaseWithAccountsAlone()
    {
        await using var db = new SqliteDatabase(seed: true);
        await db.InitializeAsync();

        await using var context = db.CreateContext();
        Assert.False(await DemoData.SeedIfEmptyAsync(context, Ct));
        Assert.Equal(2, await context.Courses.CountAsync(Ct));
    }
}
