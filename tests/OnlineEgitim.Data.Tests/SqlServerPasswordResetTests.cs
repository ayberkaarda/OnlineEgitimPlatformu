using Microsoft.Extensions.DependencyInjection;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Domain.Entities;
using static OnlineEgitim.Data.Tests.TestServices;

namespace OnlineEgitim.Data.Tests;

/// <summary>Single use codes and password changes under concurrency on a real SQL Server instance (see <see cref="SqlServerTestDatabase"/>).</summary>
public sealed class SqlServerPasswordResetTests
{
    /// <summary>Two application instances use the same code at the same moment, many times: exactly one sets its password.</summary>
    [Fact]
    public async Task OneCode_UsedTwiceAtOnce_SetsOnePassword()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var first = await Race.InitializeAsync(database, ct);
        await using var second = database.BuildServices();
        await database.ExecuteAsync("INSERT dbo.Egitmenler (AdSoyad, UzmanlikAlani, Sifre, SifreBicimi, BasarisizGiris, Rol) VALUES (N'Ayla Yönetici', N'Yönetim', N'', 2, 0, 2)", ct);
        var adminId = await database.ScalarAsync("SELECT EgitmenID FROM dbo.Egitmenler WHERE Rol = 2", ct);
        var admin = Session(AccountRole.Admin, adminId);
        var recoveries = new[] { first, second }.Select(p => p.GetRequiredService<IRecoveryService>()).ToArray();

        for (var round = 0; round < 4; round++)
        {
            var code = await first.GetRequiredService<IAdministrationService>().IssueResetCodeAsync(admin, AccountRole.Student, MertId, ct);
            var passwords = new[] { "Birinci.Sifre" + round, "Ikinci.Sifre" + round };

            var results = await Race.RunAsync(2, slot => recoveries[slot].RecoverStudentAsync("Mert Deneme", code.Code, passwords[slot], passwords[slot], ct), ct);

            var winner = Array.FindIndex(results, r => r is null);
            Assert.Single(results, r => r is null);
            Assert.Equal(RecoveryService.StudentRecoveryFailed, Assert.IsType<BusinessRuleException>(results[1 - winner]).Message);
            var auth = first.GetRequiredService<IAuthService>();
            Assert.True((await auth.SignInStudentAsync("Mert Deneme", passwords[winner], ct)).IsStudent);
            await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInStudentAsync("Mert Deneme", passwords[1 - winner], ct));
            Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Ogrenci WHERE OgrenciID = @id AND KurtarmaKodu IS NOT NULL", ct, ("@id", MertId)));
        }
    }

    /// <summary>
    /// A password change and a reset with a code race for the same account: one wins, the other writes
    /// nothing, and the password that works is the winner's.
    /// </summary>
    [Fact]
    public async Task ChangeRacingAReset_OneWins()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var provider = await Race.InitializeAsync(database, ct);
        await database.ExecuteAsync("INSERT dbo.Egitmenler (AdSoyad, UzmanlikAlani, Sifre, SifreBicimi, BasarisizGiris, Rol) VALUES (N'Ayla Yönetici', N'Yönetim', N'', 2, 0, 2)", ct);
        var admin = Session(AccountRole.Admin, await database.ScalarAsync("SELECT EgitmenID FROM dbo.Egitmenler WHERE Rol = 2", ct));
        var auth = provider.GetRequiredService<IAuthService>();
        var current = TeacherPassword;

        for (var round = 0; round < 4; round++)
        {
            var session = await auth.SignInTeacherAsync("Deniz Örnek", current, ct);
            var code = await provider.GetRequiredService<IAdministrationService>().IssueResetCodeAsync(admin, AccountRole.Teacher, DenizId, ct);
            var changed = "Degisen.Sifre" + round;
            var reset = "Sifirlanan.Sifre" + round;

            var results = await Race.RunAsync(
                2,
                slot => slot == 0
                    ? auth.ChangePasswordAsync(session, current, changed, changed, ct)
                    : provider.GetRequiredService<IRecoveryService>().RecoverTeacherAsync(code.Code, "Deniz Örnek", reset, reset, ct),
                ct);

            var working = new List<string>();
            foreach (var candidate in new[] { changed, reset })
            {
                try
                {
                    await auth.SignInTeacherAsync("Deniz Örnek", candidate, ct);
                    working.Add(candidate);
                }
                catch (BusinessRuleException)
                {
                }
            }

            // A change that ran first spends the code too, so at most one of the two writes.
            current = Assert.Single(working);
            Assert.Equal(current == changed ? 0 : 1, Array.FindIndex(results, r => r is null));
            Assert.Single(results, r => r is null);
        }
    }

    private static UserSession Session(AccountRole role, int id) => new(role, id, "yonetici");
}
