using Microsoft.EntityFrameworkCore;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Data.Persistence;

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
    public async Task Setup_CreatesTheFirstTeacherWithTheChosenPasswordOnce()
    {
        await using var db = new SqliteDatabase(seed: false);
        await db.InitializeAsync();
        var setup = db.Get<ISetupService>();

        var mismatch = await Assert.ThrowsAsync<BusinessRuleException>(
            () => setup.CreateFirstTeacherAsync("Ada Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre2", Ct));
        var weak = await Assert.ThrowsAsync<BusinessRuleException>(
            () => setup.CreateFirstTeacherAsync("Ada Kurucu", "Matematik", "kisa", "kisa", Ct));
        var session = await setup.CreateFirstTeacherAsync(" Ada Kurucu ", " Matematik ", "Gizli.Sifre1", "Gizli.Sifre1", Ct);

        Assert.Equal(SetupService.PasswordsDoNotMatch, mismatch.Message);
        Assert.Equal(AuthService.PasswordInvalid, weak.Message);
        Assert.True(session.IsTeacher);
        Assert.False(await setup.IsSetupRequiredAsync(Ct));
        Assert.Equal(session.UserId, (await db.Get<IAuthService>().SignInTeacherAsync("Ada Kurucu", "Gizli.Sifre1", Ct)).UserId);
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => setup.CreateFirstTeacherAsync("İkinci Kişi", "Fizik", "Gizli.Sifre1", "Gizli.Sifre1", Ct));
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
