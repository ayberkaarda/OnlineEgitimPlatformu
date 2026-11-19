using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Data;
using OnlineEgitim.Data.Persistence;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Theming;
using Serilog;

namespace OnlineEgitim.UI
{
    internal static class Program
    {
        internal const string ConnectionStringName = "OnlineEgitim";

        /// <summary>Application entry point and composition root.</summary>
        [STAThread]
        private static int Main()
        {
            Log.Logger = AppLogging.CreateLogger(AppLogging.DefaultDirectory);
            try
            {
                Log.Information("Application started");
                return Run();
            }
            finally
            {
                Log.Information("Application stopped");
                Log.CloseAndFlush();
            }
        }

        private static int Run()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ErrorReporter.Show(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                ErrorReporter.Show(e.ExceptionObject as Exception);
                if (e.IsTerminating)
                {
                    Log.Fatal("Unhandled exception terminates the application");
                    Log.CloseAndFlush();
                }
            };

            ApplicationConfiguration.Initialize();

            var configuration = BuildConfiguration();
            ApplyTheme(AppConfiguration.ResolveTheme(configuration));
            var connectionString = AppConfiguration.ResolveConnectionString(configuration);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Log.Warning("No database connection string is configured");
                Dialogs.Show(
                    null,
                    "Veritabanı bağlantı ayarı bulunamadı.\n\n"
                    + "appsettings.example.json dosyasını appsettings.json olarak kopyalayıp bağlantı bilgisini girin "
                    + "ya da EGITIM_CONNECTION ortam değişkenini tanımlayın.",
                    AppText.Title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }

            var options = new DataAccessOptions { SeedDemoData = AppConfiguration.IsDemoRequested(configuration) };
            using var serviceProvider = ConfigureServices(connectionString, options);

            try
            {
                // No synchronization context exists yet, so blocking here cannot deadlock.
                serviceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync().GetAwaiter().GetResult();
            }
#pragma warning disable CA1031 // Startup boundary: report and exit instead of crashing.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                ErrorReporter.Show(ex);
                return 1;
            }

            var forms = serviceProvider.GetRequiredService<IFormFactory>();

            // While the application runs, Windows may switch high contrast or the app mode; open screens follow.
            using var themeWatcher = new ThemeWatcher(
                new SystemPreferenceSource(),
                new WindowsFormsSynchronizationContext(),
                () => SystemInformation.HighContrast,
                Theme.SystemPrefersDark);
            while (true)
            {
                UserSession? session;
                try
                {
                    var stage = serviceProvider.GetRequiredService<ISetupService>().GetStageAsync().GetAwaiter().GetResult();
                    session = SignIn(forms, stage);
                }
#pragma warning disable CA1031 // Startup boundary: report and exit instead of crashing.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    ErrorReporter.Show(ex);
                    return 1;
                }

                if (session is null)
                {
                    return 0;
                }

                // A panel closed because its session ended (signed out, password changed, account
                // disabled) leads back to the sign-in; any other close ends the application.
                using var panel = CreatePanel(forms, session);
                Application.Run(panel);
                if (panel is not ISessionPanel { SignedOut: true })
                {
                    return 0;
                }
            }
        }

        /// <summary>The main window of the session's role.</summary>
        private static Form CreatePanel(IFormFactory forms, UserSession session) => session.Role switch
        {
            AccountRole.Admin => forms.Create<YoneticiForm>(session),
            AccountRole.Teacher => forms.Create<OgretmenForm>(session),
            _ => forms.Create<MainForm>(session),
        };

        /// <summary>
        /// The first-run setup (without a key) on an empty database, otherwise the sign-in, which offers
        /// the founder operations of <paramref name="stage"/>. Returns the session, or
        /// <see langword="null"/> when the user closed the window.
        /// </summary>
        private static UserSession? SignIn(IFormFactory forms, SetupStage stage)
        {
            if (stage == SetupStage.EmptyDatabase)
            {
                using var setup = forms.Create<IlkKurulumForm>();
                setup.KeyRequired = false;
                return setup.ShowDialog() == DialogResult.OK ? setup.Session : null;
            }

            using var signIn = forms.Create<GirisForm>();
            signIn.Stage = stage;
            return signIn.ShowDialog() == DialogResult.OK ? signIn.Session : null;
        }

        /// <summary>Chooses the palette; in dark mode the native parts of the controls are switched to dark too.</summary>
        private static void ApplyTheme(ThemeMode mode)
        {
            var highContrast = SystemInformation.HighContrast;
            Theme.Initialize(mode, highContrast, Theme.SystemPrefersDark());
            if (Theme.Current.IsDark && !highContrast)
            {
#pragma warning disable WFO5001 // Dark mode of the native controls is marked experimental in Windows Forms.
                Application.SetColorMode(SystemColorMode.Dark);
#pragma warning restore WFO5001
            }
        }

        private static IConfiguration BuildConfiguration() =>
            new ConfigurationBuilder()
                .AddSettingsFile(AppContext.BaseDirectory, AppConfiguration.SettingsFileName)
                .AddUserSecrets(typeof(Program).Assembly, optional: true)
                .AddEnvironmentVariables()
                .Build();

        private static ServiceProvider ConfigureServices(string connectionString, DataAccessOptions options)
        {
            var services = new ServiceCollection();
            services.AddSqlServerDataAccess(connectionString, options);
            services.AddSingleton<IAuditLog>(new SerilogAuditLog(Log.Logger));
            services.AddBusinessServices();
            services.AddSingleton<IFormFactory, FormFactory>();
            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        }
    }

    /// <summary>How the application reads its settings.</summary>
    internal static class AppConfiguration
    {
        public const string SettingsFileName = "appsettings.json";

        /// <summary>Settings key of the demonstration data switch.</summary>
        public const string DemoSettingKey = "Seed:Demo";

        /// <summary>Settings key of the palette: Light (default), Dark or System.</summary>
        public const string ThemeKey = "Appearance:Theme";

        /// <summary>The <c>Appearance:Theme</c> setting (Light, Dark or System); anything else falls back to Light.</summary>
        public static ThemeMode ResolveTheme(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var value = configuration[ThemeKey]?.Trim();

            // Only the names are accepted; Enum.TryParse would also take numbers.
            return value is { Length: > 0 } && value.All(char.IsAsciiLetter) && Enum.TryParse<ThemeMode>(value, ignoreCase: true, out var mode)
                ? mode
                : ThemeMode.Light;
        }

        /// <summary>Adds an optional JSON settings file located in <paramref name="basePath"/>.</summary>
        public static IConfigurationBuilder AddSettingsFile(this IConfigurationBuilder builder, string basePath, string fileName) =>
            builder
                .SetBasePath(basePath)
                .AddJsonFile(fileName, optional: true, reloadOnChange: false);

        /// <summary>
        /// Resolution order (last wins): appsettings.json, user secrets, ConnectionStrings__OnlineEgitim
        /// environment variable, EGITIM_CONNECTION environment variable.
        /// </summary>
        public static string? ResolveConnectionString(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var fromEnvironment = configuration[DataServiceCollectionExtensions.ConnectionEnvironmentVariable];
            return string.IsNullOrWhiteSpace(fromEnvironment)
                ? configuration.GetConnectionString(Program.ConnectionStringName)
                : fromEnvironment;
        }

        /// <summary>
        /// Whether the demonstration data was asked for, through EGITIM_DEMO ("1" or "true") or the
        /// Seed:Demo setting. Anything else, including a missing or malformed value, means no.
        /// </summary>
        public static bool IsDemoRequested(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            return IsYes(configuration[DataServiceCollectionExtensions.DemoEnvironmentVariable])
                || IsYes(configuration[DemoSettingKey]);
        }

        private static bool IsYes(string? value) =>
            value is not null
            && (value.Trim() == "1" || string.Equals(value.Trim(), "true", StringComparison.OrdinalIgnoreCase));
    }
}
