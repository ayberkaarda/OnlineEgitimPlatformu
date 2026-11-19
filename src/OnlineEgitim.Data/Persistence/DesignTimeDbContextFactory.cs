using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OnlineEgitim.Data.Persistence;

/// <summary>
/// Used by <c>dotnet ef</c> only. Reads the connection string from EGITIM_CONNECTION and falls back
/// to a local SQL Server Express instance.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EgitimDbContext>
{
    private const string FallbackConnectionString =
        "Server=localhost\\SQLEXPRESS;Database=OnlineEgitim;Trusted_Connection=True;TrustServerCertificate=True;";

    public EgitimDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(DataServiceCollectionExtensions.ConnectionEnvironmentVariable);
        var options = new DbContextOptionsBuilder<EgitimDbContext>()
            .UseSqlServer(string.IsNullOrWhiteSpace(connectionString) ? FallbackConnectionString : connectionString)
            .Options;
        return new EgitimDbContext(options);
    }
}
