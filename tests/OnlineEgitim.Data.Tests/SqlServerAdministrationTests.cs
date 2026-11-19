using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Domain.Entities;
using static OnlineEgitim.Data.Tests.TestServices;

namespace OnlineEgitim.Data.Tests;

/// <summary>
/// Administrators on a real SQL Server instance (see <see cref="SqlServerTestDatabase"/>): the last
/// administrator under concurrency, and promotion to administrator as the database owner's right only.
/// </summary>
public sealed class SqlServerAdministrationTests
{
    private const string AppUser = "OnlineEgitimAppAdminTest";
    private const string AdminPassword = "Gizli.Yonetici1";

    /// <summary>
    /// Two administrators disable each other at the same moment, many times: the administrators' lock
    /// lets exactly one of them win, so one administrator who can sign in always stays.
    /// </summary>
    [Fact]
    public async Task TwoAdminsDisableEachOther_OneAlwaysStays()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var provider = await Race.InitializeAsync(database, ct);
        await AddAdminAsync(provider, database, "Birinci Yönetici", ct);
        await AddAdminAsync(provider, database, "İkinci Yönetici", ct);
        var admin = provider.GetRequiredService<IAdministrationService>();
        var auth = provider.GetRequiredService<IAuthService>();

        for (var round = 0; round < 6; round++)
        {
            await database.ExecuteAsync("UPDATE dbo.Egitmenler SET Pasif = 0 WHERE Rol = 2", ct);
            UserSession[] sessions =
            [
                await auth.SignInTeacherAsync("Birinci Yönetici", AdminPassword, ct),
                await auth.SignInTeacherAsync("İkinci Yönetici", AdminPassword, ct),
            ];

            var results = await Race.RunAsync(
                2,
                slot => admin.SetDisabledAsync(sessions[slot], AccountRole.Admin, sessions[1 - slot].UserId, true, ct),
                ct);

            Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler WHERE Rol = 2 AND Pasif = 0", ct));
            var refused = Assert.Single(results, r => r is not null);
            Assert.Single(results, r => r is null);

            // The loser was either refused as the last administrator or, disabled first, lost its session.
            Assert.True(
                refused is SessionExpiredException
                || (refused is BusinessRuleException rule && rule.Message == AdministrationService.LastActiveAdmin),
                refused?.GetType().Name);
        }
    }

    /// <summary>
    /// The database owner makes a teacher an administrator with docs/promote-admin.sql, which also ends
    /// the teacher's sessions. The application's login, restricted by docs/grant-app-login.sql, can
    /// neither run the script nor raise a role or insert a second administrator past the services; it
    /// can still lower a role, as the administrator panel does.
    /// </summary>
    [Fact]
    public async Task Promotion_IsTheDatabaseOwnersRightOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using (var owner = await Race.InitializeAsync(database, ct))
        {
            // Teachers sign in with passwords and there is no administrator: no setup key helps.
            await Assert.ThrowsAsync<SqlException>(() => database.IssueSetupKeyAsync(ct));
            var teacherSession = await owner.GetRequiredService<IAuthService>().SignInTeacherAsync("Deniz Örnek", TeacherPassword, ct);

            await RunPromoteAsync(database, "Deniz Örnek", ct);

            Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler WHERE AdSoyad = N'Deniz Örnek' AND Rol = 2 AND OturumSurumu = 1", ct));
            await Assert.ThrowsAsync<SessionExpiredException>(() => owner.GetRequiredService<ICourseService>().GetTeachingAsync(teacherSession, ct));
            var admin = await owner.GetRequiredService<IAuthService>().SignInTeacherAsync("Deniz Örnek", TeacherPassword, ct);
            Assert.True(admin.IsAdmin);
            var twice = await Assert.ThrowsAsync<SqlException>(() => RunPromoteAsync(database, "Deniz Örnek", ct));
            Assert.Equal(50013, twice.Number);
            var unknown = await Assert.ThrowsAsync<SqlException>(() => RunPromoteAsync(database, "Olmayan Öğretmen", ct));
            Assert.Equal(50012, unknown.Number);
        }

        await database.ExecuteAsync($"CREATE USER [{AppUser}] WITHOUT LOGIN", ct);
        var grants = await File.ReadAllTextAsync(SqlServerTestDatabase.RepositoryFile("docs", "grant-app-login.sql"), ct);
        await database.ExecuteAsync(grants.Replace("N'OnlineEgitimApp'", $"N'{AppUser}'", StringComparison.Ordinal), ct);
        var unpooled = new SqlConnectionStringBuilder(database.ConnectionString) { Pooling = false }.ConnectionString;

        var script = await Assert.ThrowsAsync<SqlException>(() => RunPromoteAsync(database, "Ece Taslak", ct, asUser: AppUser));
        Assert.Equal(50010, script.Number);
        var raise = await Assert.ThrowsAsync<SqlException>(() => SqlServerTestDatabase.ExecuteAsync(
            unpooled, $"EXECUTE AS USER = N'{AppUser}'; UPDATE dbo.Egitmenler SET Rol = 2 WHERE AdSoyad = N'Ece Taslak'", ct));
        Assert.Equal(51010, raise.Number);
        var insert = await Assert.ThrowsAsync<SqlException>(() => SqlServerTestDatabase.ExecuteAsync(
            unpooled,
            $"EXECUTE AS USER = N'{AppUser}'; INSERT dbo.Egitmenler (AdSoyad, UzmanlikAlani, Sifre, SifreBicimi, BasarisizGiris, Rol) VALUES (N'Sahte Yönetici', N'x', N'', 2, 0, 2)",
            ct));
        Assert.Equal(51010, insert.Number);
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler WHERE Rol = 2", ct));

        // Lowering a role stays possible for the application's login: a second administrator, made by
        // the owner, is made a teacher again by the first one through the application.
        await RunPromoteAsync(database, "Ece Taslak", ct);
        await using var app = database.BuildServices(configureContext: b => b.AddInterceptors(new ImpersonateUser(AppUser)), pooling: false);
        var deniz = await app.GetRequiredService<IAuthService>().SignInTeacherAsync("Deniz Örnek", TeacherPassword, ct);
        await app.GetRequiredService<IAdministrationService>().DemoteAdminAsync(deniz, EceId, ct);
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler WHERE AdSoyad = N'Ece Taslak' AND Rol = 1", ct));
    }

    /// <summary>
    /// A sign-in has verified the right password and waits right before it records the success; an
    /// administrator disables the account meanwhile. The success write sees the change and the sign-in
    /// is refused with the usual message, so no session of a disabled account is issued.
    /// </summary>
    [Fact]
    public async Task SignIn_AccountDisabledAfterThePasswordCheck_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var instanceA = await Race.InitializeAsync(database, ct);
        await AddAdminAsync(instanceA, database, "Birinci Yönetici", ct);
        var gate = new SuccessWriteGate();
        await using var instanceB = database.BuildServices(services => services.HoldSuccessfulSignInWrites(gate));
        var admin = await instanceA.GetRequiredService<IAuthService>().SignInTeacherAsync("Birinci Yönetici", AdminPassword, ct);

        var signIn = Task.Run(() => instanceB.GetRequiredService<IAuthService>().SignInStudentAsync("Mert Deneme", StudentPassword, ct), ct);
        await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), ct);
        await instanceA.GetRequiredService<IAdministrationService>().SetDisabledAsync(admin, AccountRole.Student, MertId, true, ct);
        gate.Release();

        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => signIn);
        Assert.Equal(AuthService.CredentialsInvalid, refused.Message);
    }

    private static async Task RunPromoteAsync(SqlServerTestDatabase database, string teacherName, CancellationToken ct, string? asUser = null)
    {
        var script = await File.ReadAllTextAsync(SqlServerTestDatabase.RepositoryFile("docs", "promote-admin.sql"), ct);
        const string Declaration = "DECLARE @TeacherName nvarchar(100) = N'';";
        Assert.Contains(Declaration, script, StringComparison.Ordinal);
        script = script.Replace(Declaration, "DECLARE @TeacherName nvarchar(100) = @name;", StringComparison.Ordinal);
        if (asUser is not null)
        {
            script = "EXECUTE AS USER = N'" + asUser + "';\n" + script;
        }

        await using var connection = new SqlConnection(asUser is null
            ? database.ConnectionString
            : new SqlConnectionStringBuilder(database.ConnectionString) { Pooling = false }.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(script, connection);
        command.Parameters.AddWithValue("@name", teacherName);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
        }
    }

    private static async Task AddAdminAsync(ServiceProvider provider, SqlServerTestDatabase database, string name, CancellationToken ct)
    {
        var hash = provider.GetRequiredService<Business.Security.IPasswordHasher>().Hash(AdminPassword);
        await database.ExecuteAsync(
            "INSERT dbo.Egitmenler (AdSoyad, UzmanlikAlani, Sifre, SifreBicimi, BasarisizGiris, Rol) VALUES (@name, N'Yönetim', @hash, 0, 0, 2)",
            ct,
            ("@name", name),
            ("@hash", hash));
    }

    /// <summary>Runs every command of the context as a database user without a login of its own.</summary>
    private sealed class ImpersonateUser(string user) : Microsoft.EntityFrameworkCore.Diagnostics.DbConnectionInterceptor
    {
        public override async Task ConnectionOpenedAsync(System.Data.Common.DbConnection connection, Microsoft.EntityFrameworkCore.Diagnostics.ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXECUTE AS USER = @user";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@user";
            parameter.Value = user;
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
