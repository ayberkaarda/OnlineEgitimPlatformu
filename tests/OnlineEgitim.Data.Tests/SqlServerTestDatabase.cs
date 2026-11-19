using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace OnlineEgitim.Data.Tests;

/// <summary>
/// Throw-away database on a real SQL Server instance. Set EGITIM_TEST_SQLSERVER to a server-level
/// connection string to choose the instance (default: local SQL Express with Windows authentication)
/// and EGITIM_SKIP_SQL_TESTS=1 to skip every test that needs SQL Server.
/// </summary>
internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    private const string ServerEnvironmentVariable = "EGITIM_TEST_SQLSERVER";
    private const string SkipEnvironmentVariable = "EGITIM_SKIP_SQL_TESTS";
    private const string DefaultServer =
        @"Server=localhost\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5;";

    private SqlServerTestDatabase(string server, string name)
    {
        Server = server;
        Name = name;
        ConnectionString = new SqlConnectionStringBuilder(server) { InitialCatalog = name }.ConnectionString;
    }

    public string Server { get; }

    public string Name { get; }

    public string ConnectionString { get; }

    public FakeTimeProvider Clock { get; } = new(TestServices.Start);

    /// <summary>
    /// Creates an empty database with the case-insensitive Turkish collation of the original
    /// application's database, or skips the test when no SQL Server is available.
    /// </summary>
    public static async Task<SqlServerTestDatabase> CreateAsync(CancellationToken ct)
    {
        var database = await ReserveAsync(ct);
        await ExecuteAsync(database.Server, "DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@name) + N' COLLATE Turkish_CI_AS'; EXEC(@sql);", ct, ("@name", database.Name));
        return database;
    }

    /// <summary>Picks a server and a unique database name without creating the database.</summary>
    public static async Task<SqlServerTestDatabase> ReserveAsync(CancellationToken ct)
    {
        var server = await ResolveServerOrSkipAsync(ct);
        return new SqlServerTestDatabase(server, "OnlineEgitimTest_" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// The application's services on this database. Each provider has its own connection pool, so
    /// two providers behave like two application instances.
    /// </summary>
    public ServiceProvider BuildServices(
        Action<IServiceCollection>? configure = null,
        Persistence.DataAccessOptions? options = null,
        Action<Microsoft.EntityFrameworkCore.DbContextOptionsBuilder>? configureContext = null,
        bool pooling = true)
    {
        var connectionString = pooling
            ? ConnectionString
            : new SqlConnectionStringBuilder(ConnectionString) { Pooling = false }.ConnectionString;
        var services = new ServiceCollection();
        services.AddDataAccess(
            builder =>
            {
                builder.UseSqlServer(connectionString);
                configureContext?.Invoke(builder);
            },
            options);
        services.AddTestBusinessServices(Clock);
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// The application's services with a command timeout of <paramref name="seconds"/>, shorter than the
    /// application locks wait, to show that a lock wait never ends as a command timeout.
    /// </summary>
    public ServiceProvider BuildServicesWithCommandTimeout(int seconds) =>
        BuildServices(configureContext: builder => builder.UseSqlServer(ConnectionString, sql => sql.CommandTimeout(seconds)));

    /// <summary>
    /// Takes the application lock <paramref name="resource"/> on a connection of its own, as another
    /// application instance in the middle of an operation would, and keeps it until the returned
    /// connection is disposed.
    /// </summary>
    public async Task<SqlConnection> HoldAppLockAsync(string resource, CancellationToken ct)
    {
        var connection = new SqlConnection(new SqlConnectionStringBuilder(ConnectionString) { Pooling = false }.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            "DECLARE @result int; EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0; SELECT @result;",
            connection);
        command.Parameters.AddWithValue("@resource", resource);
        Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture));
        return connection;
    }

    public Task ExecuteAsync(string sql, CancellationToken ct, params (string Name, object Value)[] parameters) =>
        ExecuteAsync(ConnectionString, sql, ct, parameters);

    public Task<int> ScalarAsync(string sql, CancellationToken ct, params (string Name, object Value)[] parameters) =>
        ScalarAsync(ConnectionString, sql, ct, parameters);

    public async Task<string?> TextAsync(string sql, CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteScalarAsync(ct) as string;
    }

    public static async Task ExecuteAsync(
        string connectionString,
        string sql,
        CancellationToken ct,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync(ct);
    }

    public static async Task<int> ScalarAsync(
        string connectionString,
        string sql,
        CancellationToken ct,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Runs docs/issue-setup-key.sql as the database owner (or with <paramref name="asUser"/>
    /// impersonated) and returns the key it shows once.
    /// </summary>
    public async Task<string> IssueSetupKeyAsync(CancellationToken ct, bool revokeUnused = false, string? asUser = null)
    {
        var script = await File.ReadAllTextAsync(RepositoryFile("docs", "issue-setup-key.sql"), ct);
        if (revokeUnused)
        {
            script = script.Replace("DECLARE @RevokeUnusedKeys bit = 0;", "DECLARE @RevokeUnusedKeys bit = 1;", StringComparison.Ordinal);
        }

        if (asUser is not null)
        {
            script = "EXECUTE AS USER = N'" + asUser + "';\n" + script;
        }

        // Impersonation stays on a connection, so such a connection never goes back to the pool.
        await using var connection = new SqlConnection(asUser is null
            ? ConnectionString
            : new SqlConnectionStringBuilder(ConnectionString) { Pooling = false }.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(script, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        Assert.True(await reader.ReadAsync(ct));
        return reader.GetString(0);
    }

    /// <summary>A file of the repository, found by walking up from the test output folder.</summary>
    public static string RepositoryFile(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException("Repository file not found.", Path.Combine(parts));
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await ExecuteAsync(
            Server,
            """
            IF DB_ID(@name) IS NOT NULL
            BEGIN
                DECLARE @sql nvarchar(max) = N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@name) + N';';
                EXEC(@sql);
            END
            """,
            CancellationToken.None,
            ("@name", Name));
    }

    private static async Task<string> ResolveServerOrSkipAsync(CancellationToken ct)
    {
        var skip = Environment.GetEnvironmentVariable(SkipEnvironmentVariable);
        if (skip is "1" || string.Equals(skip, "true", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Skip($"{SkipEnvironmentVariable} is set.");
        }

        var server = Environment.GetEnvironmentVariable(ServerEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(server))
        {
            server = DefaultServer;
        }

        try
        {
            await using var connection = new SqlConnection(server);
            await connection.OpenAsync(ct);
        }
        catch (SqlException ex)
        {
            Assert.Skip($"No SQL Server available ({ex.Number}). Set {ServerEnvironmentVariable} to run this test.");
        }

        return server;
    }
}
