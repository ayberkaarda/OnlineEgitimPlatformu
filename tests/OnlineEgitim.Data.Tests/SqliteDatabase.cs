using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using OnlineEgitim.Data.Persistence;

namespace OnlineEgitim.Data.Tests;

/// <summary>
/// A private SQLite in-memory database wired through the application's own registrations. The
/// connection stays open for the lifetime of the instance, which keeps the database alive. The schema
/// comes from the EF Core model; the test data of <see cref="TestServices.SeedAsync"/> is added unless
/// <paramref name="seed"/> is false.
/// </summary>
public sealed class SqliteDatabase(bool seed = true, DataAccessOptions? options = null, Action<DbContextOptionsBuilder>? configureContext = null) : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private ServiceProvider? _provider;

    public FakeTimeProvider Clock { get; } = new(TestServices.Start);

    public IServiceProvider Services => _provider ?? throw new InvalidOperationException("Not initialized.");

    public T Get<T>()
        where T : notnull => Services.GetRequiredService<T>();

    public EgitimDbContext CreateContext() =>
        Services.GetRequiredService<IDbContextFactory<EgitimDbContext>>().CreateDbContext();

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDataAccess(
            builder =>
            {
                builder.UseSqlite(_connection);
                configureContext?.Invoke(builder);
            },
            options);
        services.AddTestBusinessServices(Clock);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        await Get<IDatabaseInitializer>().InitializeAsync();
        if (seed)
        {
            await using var db = CreateContext();
            await TestServices.SeedAsync(db, CancellationToken.None);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        await _connection.DisposeAsync();
    }
}
