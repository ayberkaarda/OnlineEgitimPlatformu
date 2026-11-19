using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineEgitim.Data.Persistence;

namespace OnlineEgitim.Data.Tests;

/// <summary>Helpers of the SQL Server race tests.</summary>
internal static class Race
{
    /// <summary>Starts every attempt at once; each result is null on success or the exception.</summary>
    public static async Task<Exception?[]> RunAsync(int count, Func<int, Task> attempt, CancellationToken ct)
    {
        using var start = new ManualResetEventSlim(false);
        var attempts = Enumerable.Range(0, count)
            .Select(slot => Task.Run(
                async () =>
                {
                    start.Wait(ct);
                    try
                    {
                        await attempt(slot);
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

    /// <summary>The application's services on a migrated database with the test data of <see cref="TestServices.SeedAsync"/>.</summary>
    public static async Task<ServiceProvider> InitializeAsync(SqlServerTestDatabase database, CancellationToken ct)
    {
        var provider = database.BuildServices();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        await using var db = await provider.GetRequiredService<IDbContextFactory<EgitimDbContext>>().CreateDbContextAsync(ct);
        await TestServices.SeedAsync(db, ct);
        return provider;
    }

    /// <summary>Adds students named "Yarışçı 0" to "Yarışçı n-1" and returns their ids in that order.</summary>
    public static async Task<int[]> AddStudentsAsync(SqlServerTestDatabase database, int count, CancellationToken ct)
    {
        var ids = new int[count];
        for (var i = 0; i < count; i++)
        {
            ids[i] = await database.ScalarAsync(
                "INSERT dbo.Ogrenci (Isim, Email, Sifre, SifreBicimi, BasarisizGiris) OUTPUT INSERTED.OgrenciID VALUES (@name, @mail, N'-', 0, 0)",
                ct,
                ("@name", "Yarışçı " + i),
                ("@mail", "yarisci" + i + "@example.com"));
        }

        return ids;
    }
}
