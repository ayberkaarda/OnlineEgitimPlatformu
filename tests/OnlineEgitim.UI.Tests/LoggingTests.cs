using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Data;
using OnlineEgitim.Data.Persistence;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI.Tests;

/// <summary>
/// What reaches the log file. The services run for real on a SQLite in-memory database; no window is
/// created by these tests.
/// </summary>
public sealed partial class LoggingTests : IDisposable
{
    // Personal data and secrets as they appear in sign-ins, registrations, recoveries and notes.
    private const string StudentName = "Deniz Örnekoğlu";
    private const string Email = "deniz.ornek@example.com";
    private const string StudentPassword = "Gizli.Ogrenci1";
    private const string TeacherName = "Ada Kurucuoğlu";
    private const string TeacherPassword = "Gizli.Ogretmen1";
    private const string Specialty = "Astrofizik";
    private const string PasswordlessName = "Şifresiz Yılmazer";
    private const string CourseName = "Kuantum Mekaniği";
    private const string Topic = "Dalga fonksiyonu";
    private const string NoteContent = "Ödev teslimi cuma saat 17.00";

    // Wednesday 5 March 2031, 10:10 UTC: the year cannot appear in a log timestamp written today.
    private static readonly DateTimeOffset Now = new(2031, 3, 5, 10, 10, 0, TimeSpan.Zero);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "onlineegitim-log-test-" + Guid.NewGuid().ToString("N"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void CreateLogger_WritesADailyFileInTheGivenDirectory()
    {
        using (var logger = AppLogging.CreateLogger(_directory))
        {
            logger.Information("probe");
        }

        var file = Assert.Single(Directory.GetFiles(_directory));
        Assert.Matches(@"onlineegitim-\d{8}\.log$", file);
        Assert.Contains("probe", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultDirectory_IsUnderLocalApplicationData()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.StartsWith(root, AppLogging.DefaultDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("OnlineEgitim", "logs"), AppLogging.DefaultDirectory, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Files dated before the last 30 days are deleted when the logger opens today's file.</summary>
    [Fact]
    public void Retention_DeletesFilesOlderThanThirtyDaysAndKeepsRecentOnes()
    {
        int[] ages = [1, 2, 15, 29, 30, 31, 45, 60, 400];
        Directory.CreateDirectory(_directory);
        foreach (var age in ages)
        {
            File.WriteAllText(DatedFile(age), "old");
        }

        var unrelated = Path.Combine(_directory, "notes.txt");
        File.WriteAllText(unrelated, "not a log file");

        using (var logger = AppLogging.CreateLogger(_directory))
        {
            logger.Information("probe");
        }

        Assert.Equal(30, AppLogging.RetainedDays);
        Assert.All(ages.Where(a => a < 30), age => Assert.True(File.Exists(DatedFile(age)), $"The file of {age} days ago was deleted."));
        Assert.All(ages.Where(a => a >= 30), age => Assert.False(File.Exists(DatedFile(age)), $"The file of {age} days ago was kept."));
        Assert.True(File.Exists(DatedFile(0)));
        Assert.True(File.Exists(unrelated));
    }

    /// <summary>
    /// More than 30 recent files (several per day, as size or restart sequences produce) are cut to the
    /// newest 30, today's file included.
    /// </summary>
    [Fact]
    public void Retention_KeepsAtMostThirtyFilesNewestFirst()
    {
        Directory.CreateDirectory(_directory);
        var created = new List<string>();
        for (var age = 1; age <= 10; age++)
        {
            created.Add(DatedFile(age));
            for (var sequence = 1; sequence <= 3; sequence++)
            {
                created.Add(DatedFile(age, sequence));
            }
        }

        foreach (var file in created)
        {
            File.WriteAllText(file, "old");
        }

        using (var logger = AppLogging.CreateLogger(_directory))
        {
            logger.Information("probe");
        }

        // Newest first: day 1 sequence 3, 2, 1, base, then day 2 and so on.
        var newestFirst = created.OrderBy(AgeOf).ThenByDescending(SequenceOf).ToList();
        var remaining = Directory.GetFiles(_directory, AppLogging.FilePrefix + "*.log");
        Assert.Equal(30, AppLogging.RetainedFileCount);
        Assert.Equal(AppLogging.RetainedFileCount, remaining.Length);
        Assert.All(newestFirst.Take(AppLogging.RetainedFileCount - 1), f => Assert.True(File.Exists(f), Path.GetFileName(f) + " was deleted."));
        Assert.All(newestFirst.Skip(AppLogging.RetainedFileCount - 1), f => Assert.False(File.Exists(f), Path.GetFileName(f) + " was kept."));
    }

    [Fact]
    public void Record_UnexpectedError_KeepsTypesCodesAndMethodsButNoMessageOrPersonalData()
    {
        var exception = Throw(new InvalidOperationException(
            $"Kayıt yazılamadı: {StudentName} {Email} şifre {StudentPassword}",
            new TimeoutException($"{CourseName} {NoteContent}")));

        var text = WriteAndRead(logger => ErrorReporter.Record(logger, exception));

        Assert.Contains("[ERR] Operation failed", text, StringComparison.Ordinal);
        Assert.Contains(typeof(InvalidOperationException).FullName!, text, StringComparison.Ordinal);
        Assert.Contains(typeof(TimeoutException).FullName!, text, StringComparison.Ordinal);
        Assert.Contains("HResult 0x", text, StringComparison.Ordinal);
        Assert.Contains(nameof(LoggingTests) + "." + nameof(Throw), text, StringComparison.Ordinal);
        Assert.DoesNotContain(".cs", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Kayıt yazılamadı", text, StringComparison.Ordinal);
        AssertNoPersonalData(text);
    }

    [Fact]
    public void Record_RefusedOperation_LogsTheTypeButNotTheMessage()
    {
        var text = WriteAndRead(logger =>
        {
            ErrorReporter.Record(logger, new BusinessRuleException($"{StudentName} {Email}"));
            ErrorReporter.Record(logger, new AccessDeniedException($"{CourseName} {NoteContent}"));
        });

        Assert.Contains("[WRN] Operation refused: BusinessRuleException", text, StringComparison.Ordinal);
        Assert.Contains("[WRN] Operation refused: AccessDeniedException", text, StringComparison.Ordinal);
        AssertNoPersonalData(text);
    }

    [Fact]
    public void AuditLog_WritesOnlyTheCodeAndTheRole()
    {
        var text = WriteAndRead(logger =>
        {
            var audit = new SerilogAuditLog(logger);
            foreach (var auditEvent in Enum.GetValues<AuditEvent>())
            {
                audit.Record(auditEvent, AccountRole.Student);
                audit.Record(auditEvent, AccountRole.Teacher);
                audit.Record(auditEvent, null);
            }
        });

        Assert.Contains("[INF] Audit auth.sign_in.failed student", text, StringComparison.Ordinal);
        Assert.Contains("[INF] Audit setup.refused none", text, StringComparison.Ordinal);
        Assert.Contains("[INF] Audit course.note.added teacher", text, StringComparison.Ordinal);
        Assert.DoesNotContain("locked", text, StringComparison.Ordinal);
        Assert.Equal(Enum.GetValues<AuditEvent>().Length * 3, Lines(text).Length);
        Assert.All(Lines(text), line => Assert.Matches(AuditLine(), line));
    }

    /// <summary>
    /// A whole working session, with every refusal recorded as the forms do: founder recovery with a
    /// setup key, registration, failed and locked sign-ins, courses, notes, enrollment and a student
    /// recovery code. The file holds fixed codes, roles and exception types only.
    /// </summary>
    [Fact]
    public async Task FullSession_LeavesOnlyCodesRolesAndTypesInTheLog()
    {
        await using var app = await App.StartAsync(_directory);
        var setupKey = await app.AddPasswordlessTeacherAndSetupKeyAsync(TeacherName, Specialty);
        var auth = app.Get<IAuthService>();
        var recovery = app.Get<IRecoveryService>();
        var courses = app.Get<ICourseService>();

        var teacher = await recovery.RecoverTeacherAsync(setupKey, TeacherName, TeacherPassword, TeacherPassword, Ct);
        await app.AttemptAsync(() => recovery.RecoverTeacherAsync(setupKey, TeacherName, TeacherPassword, TeacherPassword, Ct)); // key used, setup completed
        await app.AttemptAsync(() => app.Get<ISetupService>().CreateFirstTeacherAsync(setupKey, "İkinci Kurucu", Specialty, TeacherPassword, TeacherPassword, Ct));

        await auth.RegisterStudentAsync(StudentName, Email, StudentPassword, Ct);
        await auth.RegisterStudentAsync(StudentName, Email, StudentPassword, Ct); // taken: same outcome, no account
        await app.AttemptAsync(() => auth.RegisterStudentAsync(StudentName, "gecersiz", StudentPassword, Ct));
        for (var i = 0; i < LoginPolicy.MaxFailedAttempts; i++)
        {
            await app.AttemptAsync(() => auth.SignInStudentAsync(StudentName, "yanlis-" + StudentPassword, Ct));
        }

        await app.AttemptAsync(() => auth.SignInStudentAsync(StudentName, StudentPassword, Ct)); // locked
        app.Clock.Advance(LoginPolicy.LockoutDuration + TimeSpan.FromMinutes(1));
        var student = await auth.SignInStudentAsync(StudentName, StudentPassword, Ct);

        var courseId = await courses.CreateAsync(teacher, CourseName, Topic, Ct);
        await courses.AddNoteAsync(teacher, courseId, NoteContent, Ct);
        await app.AttemptAsync(() => courses.AddNoteAsync(student, courseId, NoteContent, Ct)); // wrong role
        await app.AttemptAsync(() => courses.AddNoteAsync(teacher, courseId + 100, NoteContent, Ct)); // not owned
        await app.Get<IEnrollmentService>().EnrollAsync(student, courseId, Ct);

        await app.AddPasswordlessStudentAsync(PasswordlessName, courseId);
        var code = await recovery.IssueStudentCodeAsync(teacher, PasswordlessName, Ct);
        await app.AttemptAsync(() => recovery.RecoverStudentAsync(PasswordlessName, "ZZZZZ-ZZZZZ", StudentPassword, StudentPassword, Ct));
        await recovery.RecoverStudentAsync(PasswordlessName, code.Code, StudentPassword, StudentPassword, Ct);

        var text = app.CloseAndRead();
        Assert.Contains("Audit recovery.completed teacher", text, StringComparison.Ordinal);
        Assert.Contains("Audit recovery.failed none", text, StringComparison.Ordinal);
        Assert.Contains("Audit auth.student.registered student", text, StringComparison.Ordinal);
        Assert.Contains("Audit auth.student.registration_not_completed student", text, StringComparison.Ordinal);
        // The refusal of the locked account is counted as one more failure, not as a lock.
        Assert.Equal(LoginPolicy.MaxFailedAttempts + 1, Regex.Count(text, "Audit auth.sign_in.failed student"));
        Assert.Contains("Audit auth.sign_in.succeeded student", text, StringComparison.Ordinal);
        Assert.Contains("Audit course.created teacher", text, StringComparison.Ordinal);
        Assert.Contains("Audit course.note.added teacher", text, StringComparison.Ordinal);
        Assert.Contains("Audit access.denied student", text, StringComparison.Ordinal);
        Assert.Contains("Audit access.denied teacher", text, StringComparison.Ordinal);
        Assert.Contains("Audit course.enrolled student", text, StringComparison.Ordinal);
        Assert.Contains("Audit recovery.code.issued teacher", text, StringComparison.Ordinal);
        Assert.Contains("Audit recovery.failed student", text, StringComparison.Ordinal);
        Assert.Contains("Audit recovery.completed student", text, StringComparison.Ordinal);
        Assert.DoesNotContain("locked", text, StringComparison.OrdinalIgnoreCase);
        Assert.All(Lines(text), line => Assert.Matches(AllowedLine(), line));
        AssertNoPersonalData(text);
        AssertAbsent(text, setupKey, SetupKeyPolicy.Format(setupKey), setupKey.ToLowerInvariant(), setupKey[..8]);
        AssertAbsent(text, code.Code, CodeFormat.Normalize(code.Code));
    }

    /// <summary>
    /// The log file is readable by the local Windows user, who knows when they made an attempt. A wrong
    /// password for an unknown, an existing, a locked and a passwordless account must leave identical
    /// entries (and show identical messages), or the file would tell which accounts exist or are locked.
    /// </summary>
    [Fact]
    public async Task SignInFailures_LeaveTheSameEntriesWhateverTheAccount()
    {
        await using var app = await App.StartAsync(Path.Combine(_directory, "setup"));
        var auth = app.Get<IAuthService>();
        await auth.RegisterStudentAsync(StudentName, Email, StudentPassword, Ct);
        await auth.RegisterStudentAsync("Kilitli Kişi", "kilitli@example.com", StudentPassword, Ct);
        for (var i = 0; i < LoginPolicy.MaxFailedAttempts; i++)
        {
            await app.AttemptAsync(() => auth.SignInStudentAsync("Kilitli Kişi", "yanlis", Ct));
        }

        await app.AddPasswordlessStudentAsync(PasswordlessName, courseId: null);
        app.CloseAndRead();

        var outcomes = new List<(string Message, string[] Entries)>();
        foreach (var name in new[] { "Bilinmeyen Kişi", StudentName, "Kilitli Kişi", PasswordlessName })
        {
            var directory = Path.Combine(_directory, outcomes.Count.ToString(CultureInfo.InvariantCulture));
            var message = await app.WithLogAsync(directory, async logger =>
            {
                var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInStudentAsync(name, "yanlis-" + StudentPassword, Ct));
                ErrorReporter.Record(logger, refused); // as the sign-in form does
                return refused.Message;
            });
            var entries = Lines(File.ReadAllText(Assert.Single(Directory.GetFiles(directory))))
                .Select(line => StampPattern().Replace(line, string.Empty))
                .ToArray();
            outcomes.Add((message, entries));
        }

        Assert.Equal(["[INF] Audit auth.sign_in.failed student", "[WRN] Operation refused: BusinessRuleException"], outcomes[0].Entries);
        Assert.All(outcomes, o => Assert.Equal(outcomes[0].Entries, o.Entries));
        Assert.All(outcomes, o => Assert.Equal(AuthService.CredentialsInvalid, o.Message));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static void AssertNoPersonalData(string text)
    {
        AssertAbsent(text, StudentName, "Deniz", "Örnekoğlu", Email, "example.com", StudentPassword, TeacherName, "Kurucuoğlu", TeacherPassword);
        AssertAbsent(text, Specialty, PasswordlessName, "Yılmazer", CourseName, "Kuantum", Topic, NoteContent, "Ödev", "2031");
        Assert.DoesNotContain("Sifre", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("şifre", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PBKDF2", text, StringComparison.Ordinal);
    }

    private static void AssertAbsent(string text, params string[] values)
    {
        foreach (var value in values)
        {
            Assert.False(text.Contains(value, StringComparison.OrdinalIgnoreCase), "The log contains a value it must not hold.");
        }
    }

    private static string[] Lines(string text) =>
        text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    private const string Stamp = @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2} ";

    [GeneratedRegex(Stamp)]
    private static partial Regex StampPattern();

    [GeneratedRegex(Stamp + @"\[INF\] Audit [a-z_]+(\.[a-z_]+)+ (student|teacher|none)$")]
    private static partial Regex AuditLine();

    [GeneratedRegex(Stamp + @"(\[INF\] Audit [a-z_]+(\.[a-z_]+)+ (student|teacher|none)|\[WRN\] Operation refused: (BusinessRuleException|AccessDeniedException))$")]
    private static partial Regex AllowedLine();

    private string WriteAndRead(Action<Serilog.ILogger> write)
    {
        using (var logger = AppLogging.CreateLogger(_directory))
        {
            write(logger);
        }

        return File.ReadAllText(Assert.Single(Directory.GetFiles(_directory)));
    }

    private string DatedFile(int age, int sequence = 0)
    {
        var day = DateTime.Today.AddDays(-age).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var suffix = sequence == 0 ? string.Empty : "_" + sequence.ToString("000", CultureInfo.InvariantCulture);
        return Path.Combine(_directory, $"{AppLogging.FilePrefix}{day}{suffix}.log");
    }

    private static int AgeOf(string file)
    {
        var day = DateTime.ParseExact(Path.GetFileName(file).Substring(AppLogging.FilePrefix.Length, 8), "yyyyMMdd", CultureInfo.InvariantCulture);
        return (DateTime.Today - day).Days;
    }

    private static int SequenceOf(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var underscore = name.LastIndexOf('_');
        return underscore < 0 ? 0 : int.Parse(name[(underscore + 1)..], CultureInfo.InvariantCulture);
    }

    private static Exception Throw(Exception exception)
    {
        try
        {
            throw exception;
        }
#pragma warning disable CA1031 // Test helper captures the stack trace.
        catch (Exception caught)
#pragma warning restore CA1031
        {
            return caught;
        }
    }

    /// <summary>
    /// The application's services on a SQLite in-memory database, with the audit events going to the
    /// current log file. Every refusal is recorded with <see cref="ErrorReporter.Record"/>, as the forms do.
    /// </summary>
    private sealed class App : IAsyncDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly SwitchableAudit _audit = new();
        private ServiceProvider? _provider;
        private Serilog.Core.Logger? _logger;
        private string _directory = string.Empty;

        public FakeTimeProvider Clock { get; } = new(Now);

        public static async Task<App> StartAsync(string directory)
        {
            var app = new App();
            await app._connection.OpenAsync(Ct);
            var services = new ServiceCollection();
            services.AddDataAccess(b => b.UseSqlite(app._connection));
            services.AddSingleton<TimeProvider>(app.Clock);
            services.AddSingleton<IPasswordHasher>(new PasswordHasher(iterations: 1_000));
            services.AddSingleton<IAuditLog>(app._audit);
            services.AddSingleton(new SetupKeyEnvironment(null));
            services.AddSingleton(new SetupKeyThrottle(TimeSpan.Zero));
            services.AddBusinessServices();
            app._provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
            await app.Get<IDatabaseInitializer>().InitializeAsync(Ct);
            app.Open(directory);
            return app;
        }

        public T Get<T>()
            where T : notnull => _provider!.GetRequiredService<T>();

        public async Task AttemptAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
#pragma warning disable CA1031 // Every failure goes to the log, as the forms do.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                ErrorReporter.Record(_logger!, ex);
            }
        }

        /// <summary>Runs <paramref name="action"/> with the audit events and its records in a log of their own.</summary>
        public async Task<T> WithLogAsync<T>(string directory, Func<Serilog.ILogger, Task<T>> action)
        {
            Open(directory);
            try
            {
                return await action(_logger!);
            }
            finally
            {
                CloseAndRead();
            }
        }

        public string CloseAndRead()
        {
            _audit.Target = null;
            _logger?.Dispose();
            _logger = null;
            return File.ReadAllText(Assert.Single(Directory.GetFiles(_directory)));
        }

        /// <summary>A teacher the original application left without a password, and a setup key issued for it.</summary>
        public async Task<string> AddPasswordlessTeacherAndSetupKeyAsync(string name, string specialty)
        {
            var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            await using var db = await Get<IDbContextFactory<EgitimDbContext>>().CreateDbContextAsync(Ct);
            db.Teachers.Add(new Teacher { FullName = name, Specialty = specialty, PasswordFormat = PasswordFormat.None });
            db.SetupKeys.Add(new SetupKey
            {
                KeyHash = SHA256.HashData(Encoding.ASCII.GetBytes(key)),
                CreatedUtc = Now.UtcDateTime,
                ExpiresUtc = Now.UtcDateTime.AddHours(24),
            });
            await db.SaveChangesAsync(Ct);
            return key;
        }

        /// <summary>A student the original application left without a password, enrolled in <paramref name="courseId"/>.</summary>
        public async Task AddPasswordlessStudentAsync(string name, int? courseId)
        {
            await using var db = await Get<IDbContextFactory<EgitimDbContext>>().CreateDbContextAsync(Ct);
            var student = new Student { FullName = name, Email = Guid.NewGuid().ToString("N") + "@kurtarma.test", PasswordFormat = PasswordFormat.None };
            db.Students.Add(student);
            await db.SaveChangesAsync(Ct);
            if (courseId is { } id)
            {
                db.Enrollments.Add(new Enrollment { StudentId = student.Id, CourseId = id });
                await db.SaveChangesAsync(Ct);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _logger?.Dispose();
            if (_provider is not null)
            {
                await _provider.DisposeAsync();
            }

            await _connection.DisposeAsync();
        }

        private void Open(string directory)
        {
            _directory = directory;
            _logger = AppLogging.CreateLogger(directory);
            _audit.Target = new SerilogAuditLog(_logger);
        }
    }

    /// <summary>Forwards audit events to the log that is open at the moment.</summary>
    private sealed class SwitchableAudit : IAuditLog
    {
        public SerilogAuditLog? Target { get; set; }

        public void Record(AuditEvent auditEvent, AccountRole? role) => Target?.Record(auditEvent, role);
    }
}
