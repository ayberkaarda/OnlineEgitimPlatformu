using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Data.Persistence;
using static OnlineEgitim.Data.Tests.TestServices;

namespace OnlineEgitim.Data.Tests;

/// <summary>
/// Concurrency on a real SQL Server instance (see <see cref="SqlServerTestDatabase"/>): one student
/// races itself into a course, failed sign-ins arrive at the same time, a success races the locking
/// failure, and two first-run setups race for the first teacher account.
/// </summary>
public sealed class SqlServerConcurrencyTests
{
    private const int Contenders = 16;

    [Fact]
    public async Task Enroll_OneStudentRacesItself_ExactlyOneEnrollment()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var provider = await InitializeAsync(database, ct);
        var service = provider.GetRequiredService<IEnrollmentService>();
        var courses = provider.GetRequiredService<ICourseService>();

        // Several rounds make it likely that the inserts really overlap.
        for (var round = 0; round < 4; round++)
        {
            var courseId = await courses.CreateAsync(Teacher(DenizId), "Yarış " + round, "Konu", ct);

            var results = await RaceAsync(Contenders, () => service.EnrollAsync(Student(MertId), courseId, ct), ct);

            Assert.Single(results, r => r is null);
            Assert.All(
                results.Where(r => r is not null),
                r => Assert.Equal("Bu dersi zaten seçtiniz.", Assert.IsType<BusinessRuleException>(r).Message));
            Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.OgrenciKurslar WHERE DersID = @id", ct, ("@id", courseId)));
        }
    }

    [Fact]
    public async Task Database_RejectsDuplicatesAndOrphansWrittenPastTheServices()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var provider = await InitializeAsync(database, ct);

        await database.ExecuteAsync("INSERT dbo.OgrenciKurslar (OgrenciID, DersID) VALUES (1, 1)", ct);

        var duplicate = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => database.ExecuteAsync("INSERT dbo.OgrenciKurslar (OgrenciID, DersID) VALUES (1, 1)", ct));
        Assert.Contains("PK_OgrenciKurslar", duplicate.Message, StringComparison.Ordinal);
        var orphan = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => database.ExecuteAsync("INSERT dbo.Notlar (DersID, Icerik) VALUES (999, N'x')", ct));
        Assert.Contains("FK_Notlar_Dersler", orphan.Message, StringComparison.Ordinal);
        var name = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => database.ExecuteAsync(
            "INSERT dbo.Egitmenler (AdSoyad, UzmanlikAlani, Sifre, SifreBicimi, BasarisizGiris) VALUES (N'DENİZ ÖRNEK', N'x', N'-', 0, 0)", ct));
        Assert.Contains("IX_Egitmenler_AdSoyad", name.Message, StringComparison.Ordinal);
        var format = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => database.ExecuteAsync("UPDATE dbo.Ogrenci SET SifreBicimi = 7 WHERE OgrenciID = 1", ct));
        Assert.Contains("CK_Ogrenci_SifreBicimi", format.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignIn_ConcurrentFailures_AreAllCountedAndLockTheAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var provider = await InitializeAsync(database, ct);
        var auth = provider.GetRequiredService<IAuthService>();

        var results = await RaceAsync(5, () => auth.SignInTeacherAsync("Deniz Örnek", "yanlis", ct), ct);
        Assert.All(results, r => Assert.IsType<BusinessRuleException>(r));

        // A read-modify-write counter would lose increments here and leave the account open.
        const string Locked = "SELECT COUNT(*) FROM dbo.Egitmenler WHERE AdSoyad = N'Deniz Örnek' AND KilitBitis IS NOT NULL AND BasarisizGiris = 0";
        Assert.Equal(1, await database.ScalarAsync(Locked, ct));
        var locked = await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInTeacherAsync("Deniz Örnek", TeacherPassword, ct));
        Assert.Equal(AuthService.CredentialsInvalid, locked.Message);
        Assert.Equal(1, await database.ScalarAsync(Locked, ct));
    }

    /// <summary>
    /// Two application instances sign in to an account with four failures: B with the right password,
    /// A with a wrong one. B is held after its password check, A's fifth failure locks the account,
    /// then B continues. B must be refused and the lock must stay.
    /// </summary>
    [Fact]
    public async Task SignIn_SuccessRacingWithFifthFailure_DoesNotClearLock()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var instanceA = await InitializeAsync(database, ct);
        var gate = new SuccessWriteGate();
        await using var instanceB = database.BuildServices(services => services.HoldSuccessfulSignInWrites(gate));
        await database.ExecuteAsync("UPDATE dbo.Ogrenci SET BasarisizGiris = 4 WHERE Isim = N'Mert Deneme'", ct);
        const string LockState = "SELECT CONVERT(nvarchar(40), KilitBitis, 126) + N'/' + CONVERT(nvarchar(10), BasarisizGiris) FROM dbo.Ogrenci WHERE Isim = N'Mert Deneme'";

        var authB = instanceB.GetRequiredService<IAuthService>();
        var signInB = Task.Run(() => authB.SignInStudentAsync("Mert Deneme", StudentPassword, ct), ct);
        await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), ct);

        var failureA = await Assert.ThrowsAsync<BusinessRuleException>(
            () => instanceA.GetRequiredService<IAuthService>().SignInStudentAsync("Mert Deneme", "yanlis", ct));
        var lockedByA = await database.TextAsync(LockState, ct);
        Assert.NotNull(lockedByA);
        Assert.EndsWith("/0", lockedByA, StringComparison.Ordinal);

        gate.Release();
        var failureB = await Assert.ThrowsAsync<BusinessRuleException>(() => signInB);

        Assert.Equal(failureA.Message, failureB.Message);
        Assert.Equal(lockedByA, await database.TextAsync(LockState, ct));
    }

    /// <summary>Two installations finish the first-run setup at the same moment: exactly one teacher is created.</summary>
    [Fact]
    public async Task Setup_ConcurrentFirstRuns_CreateExactlyOneTeacher()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        var key = new TestSetupKey(); // one founder key on the shared computer
        await using var first = database.BuildServices(s => s.AddSingleton<Business.Security.ISetupKeyStore>(key));
        await first.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        await using var second = database.BuildServices(s => s.AddSingleton<Business.Security.ISetupKeyStore>(key));

        for (var round = 0; round < 4; round++)
        {
            await database.ExecuteAsync("DELETE dbo.Egitmenler", ct);
            key.Reissue();
            var setups = new[] { first, second, first, second }.Select(p => p.GetRequiredService<ISetupService>()).ToList();
            var index = 0;

            var results = await RaceAsync(
                setups.Count,
                () =>
                {
                    var slot = Interlocked.Increment(ref index) - 1;
                    var name = "Kurucu " + (char)('A' + slot);
                    return setups[slot].CreateFirstTeacherAsync(TestSetupKey.Key, name, "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", ct);
                },
                ct);

            Assert.Single(results, r => r is null);
            Assert.All(
                results.Where(r => r is not null),
                r => Assert.Equal(SetupService.AlreadyDone, Assert.IsType<BusinessRuleException>(r).Message));
            Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler", ct));
            Assert.Equal(round + 1, key.Consumed);
        }
    }

    /// <summary>
    /// A database whose students exist but whose teachers are gone: setups without the key, racing a
    /// founder with it, never create a teacher; the founder's setup does, once.
    /// </summary>
    [Fact]
    public async Task Setup_ExistingStudents_UnauthorizedRacersCreateNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var provider = await InitializeAsync(database, ct);
        await database.ExecuteAsync("DELETE dbo.Dersler; DELETE dbo.Egitmenler", ct);
        var setup = provider.GetRequiredService<ISetupService>();
        Assert.False(await setup.IsSetupRequiredAsync(ct));
        var index = 0;

        var results = await RaceAsync(
            Contenders,
            () =>
            {
                var slot = Interlocked.Increment(ref index) - 1;
                var key = slot == Contenders / 2 ? TestSetupKey.Key : "YANLIS-" + slot;
                return setup.CreateFirstTeacherAsync(key, "Kurucu " + (char)('A' + slot), "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", ct);
            },
            ct);

        Assert.Single(results, r => r is null);
        Assert.All(
            results.Where(r => r is not null),
            r => Assert.Contains(Assert.IsType<BusinessRuleException>(r).Message, new[] { SetupService.SetupKeyInvalid, SetupService.AlreadyDone }));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler WHERE AdSoyad = @name", ct, ("@name", "Kurucu " + (char)('A' + (Contenders / 2)))));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler", ct));
        Assert.Equal(1, provider.SetupKey().Consumed);
    }

    /// <summary>
    /// The demo seed has found the database empty and is about to write its accounts when a founder's
    /// setup starts on a second instance. The setup must wait for the seed and then find a teacher; the
    /// two may never both write (demo accounts next to a real teacher).
    /// </summary>
    [Fact]
    public async Task DemoSeed_SetupBetweenCheckAndWrite_DoesNotMix()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var setupInstance = database.BuildServices();
        Task<UserSession>? setup = null;
        var hook = new SaveHook(
            e => e is Domain.Entities.Teacher,
            async token =>
            {
                setup = Task.Run(
                    () => setupInstance.GetRequiredService<ISetupService>()
                        .CreateFirstTeacherAsync(TestSetupKey.Key, "Gerçek Kurucu", "Matematik", "Gizli.Sifre1", "Gizli.Sifre1", token),
                    token);
                await WaitUntilBlockedOrDoneAsync(database, setup, token);
            });
        await using var demoInstance = database.BuildServices(
            options: new DataAccessOptions { SeedDemoData = true },
            configureContext: b => b.AddInterceptors(hook));

        await demoInstance.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);

        Assert.True(hook.Fired);
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => setup!);
        Assert.Equal(SetupService.AlreadyDone, refused.Message);
        Assert.Equal(2, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler", ct));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler WHERE AdSoyad = N'Gerçek Kurucu'", ct));
        Assert.Equal(3, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Dersler", ct));
        Assert.Equal(0, setupInstance.SetupKey().Consumed);
    }

    /// <summary>
    /// Waits until a request on the test database is blocked by another session (the setup waiting for
    /// the seed's locks) or <paramref name="other"/> finished without waiting.
    /// </summary>
    private static async Task WaitUntilBlockedOrDoneAsync(SqlServerTestDatabase database, Task other, CancellationToken ct)
    {
        const string Blocked = "SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id <> 0 AND database_id = DB_ID(@name)";
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!other.IsCompleted && DateTime.UtcNow < deadline)
        {
            if (await SqlServerTestDatabase.ScalarAsync(database.Server, Blocked, ct, ("@name", database.Name)) > 0)
            {
                return;
            }

            await Task.Delay(20, ct);
        }
    }

    private static async Task<ServiceProvider> InitializeAsync(SqlServerTestDatabase database, CancellationToken ct)
    {
        var provider = database.BuildServices();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        await using var db = await provider.GetRequiredService<IDbContextFactory<EgitimDbContext>>().CreateDbContextAsync(ct);
        await SeedAsync(db, ct);
        return provider;
    }

    /// <summary>Starts every attempt at once; each result is null on success or the exception.</summary>
    private static async Task<Exception?[]> RaceAsync(int count, Func<Task> attempt, CancellationToken ct)
    {
        using var start = new ManualResetEventSlim(false);
        var attempts = Enumerable.Range(0, count)
            .Select(_ => Task.Run(
                async () =>
                {
                    start.Wait(ct);
                    try
                    {
                        await attempt();
                        return (Exception?)null;
                    }
#pragma warning disable CA1031 // Collected and asserted by the caller.
                    catch (Exception ex)
#pragma warning restore CA1031
                    {
                        return ex;
                    }
                },
                ct))
            .ToList();
        start.Set();
        return await Task.WhenAll(attempts);
    }
}
