using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Data;
using OnlineEgitim.Data.Persistence;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    internal static class Program
    {
        internal const string ConnectionStringName = "OnlineEgitim";

        /// <summary>Application entry point and composition root.</summary>
        [STAThread]
        private static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ErrorReporter.Show(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => ErrorReporter.Show(e.ExceptionObject as Exception);

            ApplicationConfiguration.Initialize();

            var configuration = BuildConfiguration();
            var connectionString = AppConfiguration.ResolveConnectionString(configuration);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Dialogs.Show(
                    null,
                    "Veritabanı bağlantı ayarı bulunamadı.\n\n"
                    + "appsettings.example.json dosyasını appsettings.json olarak kopyalayıp bağlantı bilgisini girin "
                    + "ya da EGITIM_CONNECTION ortam değişkenini tanımlayın.",
                    AppText.Title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            var options = new DataAccessOptions { SeedDemoData = AppConfiguration.IsDemoRequested(configuration) };
            using var serviceProvider = ConfigureServices(connectionString, options);

            UserSession? session;
            try
            {
                // No synchronization context exists yet, so blocking here cannot deadlock.
                serviceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync().GetAwaiter().GetResult();
                var setup = serviceProvider.GetRequiredService<ISetupService>();
                var setupRequired = setup.IsSetupRequiredAsync().GetAwaiter().GetResult();
                var teacherMissing = setupRequired || setup.IsTeacherMissingAsync().GetAwaiter().GetResult();
                session = SignIn(serviceProvider.GetRequiredService<IFormFactory>(), setupRequired, teacherMissing);
            }
#pragma warning disable CA1031 // Startup boundary: report and exit instead of crashing.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                ErrorReporter.Show(ex);
                return;
            }

            if (session is null)
            {
                return;
            }

            var forms = serviceProvider.GetRequiredService<IFormFactory>();
            Application.Run(session.IsTeacher ? forms.Create<OgretmenForm>(session) : forms.Create<MainForm>(session));
        }

        /// <summary>
        /// The first-run setup on an empty database, otherwise the sign-in; a database with accounts
        /// but no teacher offers the setup (with the setup key) from the sign-in. Returns the session,
        /// or <see langword="null"/> when the user closed the window.
        /// </summary>
        private static UserSession? SignIn(IFormFactory forms, bool setupRequired, bool teacherMissing)
        {
            if (setupRequired)
            {
                using var setup = forms.Create<IlkKurulumForm>();
                return setup.ShowDialog() == DialogResult.OK ? setup.Session : null;
            }

            using var signIn = forms.Create<GirisForm>();
            signIn.OfferTeacherSetup = teacherMissing;
            return signIn.ShowDialog() == DialogResult.OK ? signIn.Session : null;
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
            services.AddSingleton<IAuditLog, TraceAuditLog>();
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
