using Microsoft.Extensions.Configuration;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Data;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI.Tests;

public sealed class ConfigurationTests
{
    private const string ExampleFileName = "appsettings.example.json";

    [Fact]
    public void ExampleConfiguration_IsValidJsonWithANamedInstanceAndNoDemo()
    {
        Assert.True(
            File.Exists(Path.Combine(AppContext.BaseDirectory, ExampleFileName)),
            $"{ExampleFileName} was not copied next to the tests.");

        // Same loader the application uses for appsettings.json; a malformed file throws here.
        var configuration = new ConfigurationBuilder()
            .AddSettingsFile(AppContext.BaseDirectory, ExampleFileName)
            .Build();

        var connectionString = AppConfiguration.ResolveConnectionString(configuration);

        Assert.NotNull(connectionString);
        Assert.Contains(@"Server=localhost\SQLEXPRESS;", connectionString, StringComparison.Ordinal);
        Assert.Contains("Database=OnlineEgitim;", connectionString, StringComparison.Ordinal);
        Assert.DoesNotContain(@"\\", connectionString, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.False(AppConfiguration.IsDemoRequested(configuration));
    }

    [Fact]
    public void EnvironmentVariable_OverridesSettingsFile()
    {
        var configuration = Build(new()
        {
            ["ConnectionStrings:OnlineEgitim"] = "Server=from-file;",
            [DataServiceCollectionExtensions.ConnectionEnvironmentVariable] = "Server=from-environment;",
        });

        Assert.Equal("EGITIM_CONNECTION", DataServiceCollectionExtensions.ConnectionEnvironmentVariable);
        Assert.Equal("Server=from-environment;", AppConfiguration.ResolveConnectionString(configuration));
    }

    [Fact]
    public void SettingsFile_IsUsedWithoutEnvironmentVariable() =>
        Assert.Equal("Server=from-file;", AppConfiguration.ResolveConnectionString(Build(new() { ["ConnectionStrings:OnlineEgitim"] = "Server=from-file;" })));

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("0", null, false)]
    [InlineData("yes", null, false)]
    [InlineData("1", null, true)]
    [InlineData(" TRUE ", null, true)]
    [InlineData(null, "true", true)]
    [InlineData(null, "false", false)]
    public void Demo_IsOnOnlyWhenAskedForExplicitly(string? environment, string? setting, bool expected)
    {
        var configuration = Build(new()
        {
            [DataServiceCollectionExtensions.DemoEnvironmentVariable] = environment,
            [AppConfiguration.DemoSettingKey] = setting,
        });

        Assert.Equal("EGITIM_DEMO", DataServiceCollectionExtensions.DemoEnvironmentVariable);
        Assert.Equal(expected, AppConfiguration.IsDemoRequested(configuration));
    }

    [Fact]
    public void AuditTrace_CarriesOnlyTheCodeAndTheRole() =>
        Assert.Equal("audit auth.sign_in.failed role=teacher", TraceAuditLog.Format(AuditEvent.SignInFailed, AccountRole.Teacher));

    private static IConfiguration Build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
