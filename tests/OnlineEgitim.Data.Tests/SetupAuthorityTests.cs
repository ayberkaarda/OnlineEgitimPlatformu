using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Data.Persistence;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Data.Tests;

/// <summary>
/// The setup key's authority lives in the database (see <see cref="SqlServerTestDatabase"/>): only the
/// database owner creates keys, with docs/issue-setup-key.sql; the application's login, restricted by
/// docs/grant-app-login.sql, can only check a key and mark it used.
/// </summary>
public sealed class SetupAuthorityTests
{
    private const string AppUser = "OnlineEgitimAppTest";

    private const string TeacherState = "SELECT CONCAT(Sifre, N'|', SifreBicimi) FROM dbo.Egitmenler WHERE AdSoyad = @name";

    /// <summary>
    /// Two application instances stand for two Windows profiles or computers on one database. While a
    /// teacher signs in with a password, no profile gets a key accepted and the owner's script creates
    /// none. Once no teacher has a password, a profile without the owner's key cannot set the password
    /// of a passwordless teacher (there is no way to make a key in the application), and a key used by
    /// one profile cannot be used again from the other, also not through EGITIM_SETUP_KEY.
    /// </summary>
    [Fact]
    public async Task SetupAuthority_UntrustedProfileCannotRecoverTeacher()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var founder = await InitializeAsync(database, ct);
        await database.ExecuteAsync(
            "INSERT dbo.Egitmenler (AdSoyad, UzmanlikAlani, Sifre, SifreBicimi, BasarisizGiris) VALUES (N'Şifresiz Öğretmen', N'Fizik', N'', 2, 0), (N'İkinci Şifresiz', N'Kimya', N'', 2, 0)",
            ct);

        // An environment value only fills in a form; it is checked like any typed key.
        var inventedKey = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        await using var untrusted = database.BuildServices(s => s.AddSingleton(new SetupKeyEnvironment(inventedKey)));
        var untrustedRecovery = untrusted.GetRequiredService<IRecoveryService>();
        var prefill = untrusted.GetRequiredService<ISetupService>().GetEnvironmentKey();
        Assert.False(prefill.Invalid);
        var before = await database.TextAsync(TeacherState, ct, ("@name", "Şifresiz Öğretmen"));

        // A teacher with a password exists: no key is created or accepted.
        await Assert.ThrowsAsync<SqlException>(() => database.IssueSetupKeyAsync(ct));
        var completed = await Assert.ThrowsAsync<BusinessRuleException>(
            () => untrustedRecovery.RecoverTeacherAsync(prefill.Key, "Şifresiz Öğretmen", "Ele.Gecir1", "Ele.Gecir1", ct));
        Assert.Equal(SetupService.SetupKeyNotAllowed, completed.Message);

        // No teacher has a password any more; the untrusted profile still has no key of the database.
        await database.ExecuteAsync("UPDATE dbo.Egitmenler SET Sifre = N'', SifreBicimi = 2", ct);
        before = await database.TextAsync(TeacherState, ct, ("@name", "Şifresiz Öğretmen"));
        var invented = await Assert.ThrowsAsync<BusinessRuleException>(
            () => untrustedRecovery.RecoverTeacherAsync(prefill.Key, "Şifresiz Öğretmen", "Ele.Gecir1", "Ele.Gecir1", ct));
        Assert.Equal(RecoveryService.TeacherRecoveryFailed, invented.Message);
        Assert.Equal(before, await database.TextAsync(TeacherState, ct, ("@name", "Şifresiz Öğretmen")));

        // The founder's key, from the owner's script, works once from the founder's profile ...
        var key = await database.IssueSetupKeyAsync(ct);
        var recovered = await founder.GetRequiredService<IRecoveryService>().RecoverTeacherAsync(key, "Şifresiz Öğretmen", "Kurucu.Sifre1", "Kurucu.Sifre1", ct);
        Assert.True(recovered.IsTeacher);

        // ... and not again from another one, neither typed nor from the environment.
        await using var other = database.BuildServices(s => s.AddSingleton(new SetupKeyEnvironment(key)));
        var otherPrefill = other.GetRequiredService<ISetupService>().GetEnvironmentKey();
        Assert.NotNull(otherPrefill.Key);
        var reused = await Assert.ThrowsAsync<BusinessRuleException>(
            () => other.GetRequiredService<IRecoveryService>().RecoverTeacherAsync(otherPrefill.Key, "İkinci Şifresiz", "Ele.Gecir1", "Ele.Gecir1", ct));
        Assert.Equal(SetupService.SetupKeyNotAllowed, reused.Message); // a teacher has a password now
        await database.ExecuteAsync("UPDATE dbo.Egitmenler SET Sifre = N'', SifreBicimi = 2 WHERE AdSoyad = N'Şifresiz Öğretmen'", ct);
        var spent = await Assert.ThrowsAsync<BusinessRuleException>(
            () => other.GetRequiredService<IRecoveryService>().RecoverTeacherAsync(key, "İkinci Şifresiz", "Ele.Gecir1", "Ele.Gecir1", ct));
        Assert.Equal(RecoveryService.TeacherRecoveryFailed, spent.Message);
        Assert.Equal("|2", await database.TextAsync(TeacherState, ct, ("@name", "İkinci Şifresiz")));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.KurulumAnahtarlari WHERE Tuketildi = 1", ct));
    }

    /// <summary>
    /// The application's login, with the rights of docs/grant-app-login.sql, cannot create, revoke,
    /// extend or delete a setup key, nor run the issuing script; it can still start, check the owner's
    /// key and use it up in the write it authorizes.
    /// </summary>
    [Fact]
    public async Task SetupKey_AppLoginCannotIssueOrAlterKeys()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using (var owner = await InitializeAsync(database, ct))
        {
            await database.ExecuteAsync("UPDATE dbo.Egitmenler SET Sifre = N'', SifreBicimi = 2", ct);
        }

        await database.ExecuteAsync($"CREATE USER [{AppUser}] WITHOUT LOGIN", ct);
        var grants = await File.ReadAllTextAsync(SqlServerTestDatabase.RepositoryFile("docs", "grant-app-login.sql"), ct);
        await database.ExecuteAsync(grants.Replace("N'OnlineEgitimApp'", $"N'{AppUser}'", StringComparison.Ordinal), ct);
        var key = await database.IssueSetupKeyAsync(ct);
        const string Row = "SELECT CONCAT(CONVERT(nvarchar(64), AnahtarOzeti, 2), N'|', CONVERT(nvarchar(30), SonKullanma, 126), N'|', Tuketildi) FROM dbo.KurulumAnahtarlari";
        var row = await database.TextAsync(Row, ct);

        string[] forbidden =
        [
            "INSERT dbo.KurulumAnahtarlari (AnahtarOzeti, Olusturma, SonKullanma, Tuketildi) VALUES (HASHBYTES('SHA2_256', 'X'), SYSUTCDATETIME(), DATEADD(DAY, 1, SYSUTCDATETIME()), 0)",
            "UPDATE dbo.KurulumAnahtarlari SET SonKullanma = DATEADD(YEAR, 1, SonKullanma)",
            "UPDATE dbo.KurulumAnahtarlari SET AnahtarOzeti = HASHBYTES('SHA2_256', 'X')",
            "DELETE dbo.KurulumAnahtarlari",
        ];
        // Impersonation stays on a connection, so these never go back to the pool.
        var unpooled = new SqlConnectionStringBuilder(database.ConnectionString) { Pooling = false }.ConnectionString;
        foreach (var statement in forbidden)
        {
            var denied = await Assert.ThrowsAsync<SqlException>(() => SqlServerTestDatabase.ExecuteAsync(unpooled, $"EXECUTE AS USER = N'{AppUser}'; {statement}", ct));
            Assert.True(denied.Number is 229 or 230, $"Permission denied was expected, got {denied.Number}: {statement}"); // 229 object, 230 column
        }

        await Assert.ThrowsAsync<SqlException>(() => database.IssueSetupKeyAsync(ct, asUser: AppUser));
        Assert.Equal(row, await database.TextAsync(Row, ct));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.KurulumAnahtarlari", ct));

        // The application under that login starts and uses the owner's key once.
        await using var app = database.BuildServices(configureContext: b => b.AddInterceptors(new ImpersonateUser(AppUser)), pooling: false);
        await app.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        var teacher = await app.GetRequiredService<IRecoveryService>().RecoverTeacherAsync(key, "Deniz Örnek", "Yeni.Sifre1", "Yeni.Sifre1", ct);
        Assert.Equal(DenizId, teacher.UserId);
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.KurulumAnahtarlari WHERE Tuketildi = 1 AND TuketimTarihi IS NOT NULL", ct));
        Assert.Equal(SetupStage.Completed, await app.GetRequiredService<ISetupService>().GetStageAsync(ct));
    }

    private const int DenizId = TestServices.DenizId;

    private static async Task<ServiceProvider> InitializeAsync(SqlServerTestDatabase database, CancellationToken ct)
    {
        var provider = database.BuildServices();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        await using var db = await provider.GetRequiredService<IDbContextFactory<EgitimDbContext>>().CreateDbContextAsync(ct);
        await TestServices.SeedAsync(db, ct);
        return provider;
    }

    /// <summary>Runs every command of the context as a database user without a login of its own.</summary>
    private sealed class ImpersonateUser(string user) : DbConnectionInterceptor
    {
        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            await using var command = Impersonate(connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            using var command = Impersonate(connection);
            command.ExecuteNonQuery();
        }

        private DbCommand Impersonate(DbConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "EXECUTE AS USER = @user";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@user";
            parameter.Value = user;
            command.Parameters.Add(parameter);
            return command;
        }
    }
}
