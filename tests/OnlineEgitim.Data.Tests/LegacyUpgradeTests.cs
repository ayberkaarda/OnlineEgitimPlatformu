using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Data.Persistence;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Data.Tests;

/// <summary>Runs against a real SQL Server instance; see <see cref="SqlServerTestDatabase"/>.</summary>
public sealed class LegacyUpgradeTests
{
    private const string BackupEnvironmentVariable = "EGITIM_LEGACY_BACKUP";

    // Schema of the original application's database (read from its backup): system-named primary and
    // foreign keys, nullable student names, e-mail addresses and passwords, plain text passwords in
    // NVARCHAR(100), no unique constraint besides the enrollment key, no migration history.
    internal const string LegacySchema = """
        CREATE TABLE dbo.Egitmenler (
            EgitmenID INT IDENTITY(1,1) PRIMARY KEY,
            AdSoyad NVARCHAR(100) NOT NULL,
            UzmanlikAlani NVARCHAR(100) NOT NULL,
            Sifre NVARCHAR(100) NULL);
        CREATE TABLE dbo.Ogrenci (
            OgrenciID INT IDENTITY(1,1) PRIMARY KEY,
            Isim NVARCHAR(100) NULL,
            Email NVARCHAR(100) NULL,
            Sifre NVARCHAR(100) NULL);
        CREATE TABLE dbo.Dersler (
            DersID INT IDENTITY(1,1) PRIMARY KEY,
            DersAdi NVARCHAR(100) NOT NULL,
            EgitmenID INT NOT NULL FOREIGN KEY REFERENCES dbo.Egitmenler (EgitmenID),
            Konu NVARCHAR(200) NULL);
        CREATE TABLE dbo.Notlar (
            NotID INT IDENTITY(1,1) PRIMARY KEY,
            DersID INT NOT NULL FOREIGN KEY REFERENCES dbo.Dersler (DersID),
            Icerik NVARCHAR(500) NOT NULL);
        CREATE TABLE dbo.OgrenciKurslar (
            OgrenciID INT NOT NULL FOREIGN KEY REFERENCES dbo.Ogrenci (OgrenciID),
            DersID INT NOT NULL FOREIGN KEY REFERENCES dbo.Dersler (DersID),
            PRIMARY KEY (OgrenciID, DersID));
        """;

    // Fictional rows. Identity values deliberately do not start at 1 so overwritten or re-seeded rows
    // would show. "1234" is what the original course form gave every teacher it created; a NULL and an
    // empty password are what the original application left on accounts that never had one.
    private const string LegacyData = """
        SET IDENTITY_INSERT dbo.Egitmenler ON;
        INSERT dbo.Egitmenler (EgitmenID, AdSoyad, UzmanlikAlani, Sifre) VALUES
            (3, N'Eski Öğretmen', N'Matematik', N'1234'),
            (5, N'Diğer Öğretmen', N'Fizik', N'fizik99');
        SET IDENTITY_INSERT dbo.Egitmenler OFF;
        SET IDENTITY_INSERT dbo.Ogrenci ON;
        INSERT dbo.Ogrenci (OgrenciID, Isim, Email, Sifre) VALUES
            (2, N'Eski Öğrenci', N'eski@example.com', N'abcd'),
            (4, N'Şifresiz Öğrenci', N'sifresiz@example.com', NULL),
            (6, N'Boş Şifreli', N'bos@example.com', N'');
        SET IDENTITY_INSERT dbo.Ogrenci OFF;
        SET IDENTITY_INSERT dbo.Dersler ON;
        INSERT dbo.Dersler (DersID, DersAdi, EgitmenID, Konu) VALUES
            (3, N'Eski Ders', 3, N'Kesirler'),
            (4, N'Fizik Dersi', 5, NULL);
        SET IDENTITY_INSERT dbo.Dersler OFF;
        SET IDENTITY_INSERT dbo.Notlar ON;
        INSERT dbo.Notlar (NotID, DersID, Icerik) VALUES (2, 3, N'Eski not');
        SET IDENTITY_INSERT dbo.Notlar OFF;
        INSERT dbo.OgrenciKurslar (OgrenciID, DersID) VALUES (2, 3), (4, 4);
        """;

    [Fact]
    public async Task Initialize_LegacyDatabase_PreservesDataAndKeepsOwnership()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.CreateAsync(ct);
        await database.ExecuteAsync(LegacySchema, ct);
        await database.ExecuteAsync(LegacyData, ct);

        await using var provider = database.BuildServices();
        var initializer = provider.GetRequiredService<IDatabaseInitializer>();
        await initializer.InitializeAsync(ct);
        await initializer.InitializeAsync(ct); // Second start must be a no-op.

        // Existing rows and identities are kept; nothing else was added.
        Assert.Equal(2, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler WHERE EgitmenID IN (3, 5)", ct));
        Assert.Equal(3, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Ogrenci WHERE OgrenciID IN (2, 4, 6)", ct));
        Assert.Equal(2, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Dersler", ct));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Notlar WHERE NotID = 2", ct));
        Assert.Equal(2, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.OgrenciKurslar", ct));
        Assert.Equal(2, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler WHERE SifreBicimi = 1 AND BasarisizGiris = 0 AND KilitBitis IS NULL", ct));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Ogrenci WHERE SifreBicimi = 1", ct));
        Assert.Equal(2, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Ogrenci WHERE SifreBicimi = 2 AND Sifre = N''", ct));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE N'%_InitialCreate'", ct));
        await AssertUpgradedSchemaAsync(database, ct);

        // Plain text passwords still work once and are replaced with hashes; accounts without a
        // password stay unable to sign in, as they were.
        var auth = provider.GetRequiredService<IAuthService>();
        var teacher = await auth.SignInTeacherAsync("Eski Öğretmen", "1234", ct);
        var student = await auth.SignInStudentAsync("Eski Öğrenci", "abcd", ct);
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler WHERE EgitmenID = 3 AND Sifre LIKE N'PBKDF2-SHA256$%' AND SifreBicimi = 0", ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInStudentAsync("Şifresiz Öğrenci", "x", ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInStudentAsync("Boş Şifreli", " ", ct));

        // The upgraded courses belong to their teachers: each sees only their own.
        var courses = provider.GetRequiredService<ICourseService>();
        var enrollments = provider.GetRequiredService<IEnrollmentService>();
        Assert.Equal([3], (await courses.GetTeachingAsync(teacher, ct)).Select(c => c.Id));
        Assert.Equal(["Eski Öğrenci"], await enrollments.GetStudentsAsync(teacher, 3, ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => enrollments.GetStudentsAsync(teacher, 4, ct));
        var details = await courses.GetDetailsAsync(student, 3, ct);
        Assert.Equal(["Eski not"], details.Notes);
        Assert.Equal([3], (await enrollments.GetMyCoursesAsync(student, ct)).Select(c => c.Id));

        // New rows work on the upgraded schema and continue after the existing identities.
        var courseId = await courses.CreateAsync(teacher, "Yeni Ders", "Yeni Konu", ct);
        Assert.True(courseId > 4);
        await courses.AddNoteAsync(teacher, courseId, "Yeni not", ct);
        await enrollments.EnrollAsync(student, courseId, ct);
        await auth.RegisterStudentAsync("Yeni Öğrenci", "yeni@example.com", "Gizli.Sifre1", ct);
        Assert.True(await database.ScalarAsync("SELECT OgrenciID FROM dbo.Ogrenci WHERE Isim = N'Yeni Öğrenci'", ct) > 6);
        Assert.False(await provider.GetRequiredService<ISetupService>().IsSetupRequiredAsync(ct));

        // The database itself refuses rows that break the new constraints.
        var duplicate = await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync(
            "INSERT dbo.Ogrenci (Isim, Email, Sifre, SifreBicimi, BasarisizGiris) VALUES (N'eski öğrenci', N'baska@example.com', N'-', 0, 0)",
            ct));
        Assert.Contains("IX_Ogrenci_Isim", duplicate.Message, StringComparison.Ordinal);
        var orphan = await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync(
            "INSERT dbo.Dersler (DersAdi, EgitmenID) VALUES (N'Sahipsiz', 999)",
            ct));
        Assert.Contains("FK_Dersler_Egitmenler", orphan.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Accounts that the original application left without a password never sign in, with an empty or
    /// any other password. A student gets a first password with a code issued by a teacher, a teacher
    /// with the setup key; refused attempts change nothing and each account keeps its id and courses.
    /// </summary>
    [Fact]
    public async Task PasswordlessAccount_VerifiedRecovery_PreservesIdentityAndEnrollments()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.CreateAsync(ct);
        await database.ExecuteAsync(LegacySchema, ct);
        await database.ExecuteAsync(LegacyData + "UPDATE dbo.Egitmenler SET Sifre = NULL WHERE EgitmenID = 5;", ct);
        await using var provider = database.BuildServices();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        var auth = provider.GetRequiredService<IAuthService>();
        var recovery = provider.GetRequiredService<IRecoveryService>();
        const string Student = "SELECT CONCAT(Sifre, N'|', SifreBicimi, N'|', KurtarmaKodu, N'|', KurtarmaBitis) FROM dbo.Ogrenci WHERE OgrenciID = 4";
        const string Teacher = "SELECT CONCAT(Sifre, N'|', SifreBicimi) FROM dbo.Egitmenler WHERE EgitmenID = 5";

        foreach (var password in new[] { " ", "x", "Gizli.Sifre9", Guid.NewGuid().ToString("N") })
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInStudentAsync("Şifresiz Öğrenci", password, ct));
            await Assert.ThrowsAsync<BusinessRuleException>(() => auth.SignInTeacherAsync("Diğer Öğretmen", password, ct));
        }

        await database.ExecuteAsync("UPDATE dbo.Ogrenci SET BasarisizGiris = 0 WHERE OgrenciID = 4; UPDATE dbo.Egitmenler SET BasarisizGiris = 0 WHERE EgitmenID = 5", ct);
        var studentBefore = await database.TextAsync(Student, ct);
        var teacherBefore = await database.TextAsync(Teacher, ct);
        Assert.Equal("|2||", studentBefore);
        Assert.Equal("|2", teacherBefore);

        // Without a code or the key nothing changes.
        await Assert.ThrowsAsync<BusinessRuleException>(() => recovery.RecoverStudentAsync("Şifresiz Öğrenci", "ABCDE-FGHJK", "Yeni.Sifre1", "Yeni.Sifre1", ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => recovery.RecoverTeacherAsync("YANLIS-ANAHTAR", "Diğer Öğretmen", "Yeni.Sifre1", "Yeni.Sifre1", ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => recovery.RecoverTeacherAsync(Convert.ToHexString(new byte[16]), "Diğer Öğretmen", "Yeni.Sifre1", "Yeni.Sifre1", ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => recovery.IssueStudentCodeAsync(TestServices.Student(2), "Şifresiz Öğrenci", ct));
        Assert.Equal(studentBefore, await database.TextAsync(Student, ct));
        Assert.Equal(teacherBefore, await database.TextAsync(Teacher, ct));

        // While a teacher signs in with a password ("1234"), no key is created or accepted.
        var refusedIssue = await Assert.ThrowsAsync<SqlException>(() => database.IssueSetupKeyAsync(ct));
        Assert.Equal(50001, refusedIssue.Number);
        Assert.Equal(SetupStage.Completed, await provider.GetRequiredService<ISetupService>().GetStageAsync(ct));

        // Once no teacher has a password, the database owner's key sets the first password, once.
        await database.ExecuteAsync("UPDATE dbo.Egitmenler SET Sifre = N'', SifreBicimi = 2 WHERE EgitmenID = 3", ct);
        Assert.Equal(SetupStage.TeacherPasswordNeedsKey, await provider.GetRequiredService<ISetupService>().GetStageAsync(ct));
        var key = await database.IssueSetupKeyAsync(ct);
        Assert.Matches("^[0-9A-F]{4}(-[0-9A-F]{4}){7}$", key);
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.KurulumAnahtarlari WHERE CONVERT(nvarchar(200), AnahtarOzeti, 2) LIKE N'%' + @key + N'%'", ct, ("@key", key.Replace("-", string.Empty, StringComparison.Ordinal))));
        var teacher = await recovery.RecoverTeacherAsync(key.ToLowerInvariant(), "Diğer Öğretmen", "Yeni.Sifre2", "Yeni.Sifre2", ct);
        Assert.Equal(5, teacher.UserId);
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler WHERE EgitmenID = 5 AND SifreBicimi = 0 AND Sifre LIKE N'PBKDF2-SHA256$%'", ct));
        Assert.Equal([4], (await provider.GetRequiredService<ICourseService>().GetTeachingAsync(teacher, ct)).Select(c => c.Id));
        Assert.Equal(5, (await auth.SignInTeacherAsync("Diğer Öğretmen", "Yeni.Sifre2", ct)).UserId);
        await Assert.ThrowsAsync<BusinessRuleException>(() => recovery.RecoverTeacherAsync(key, "Eski Öğretmen", "Baska.Sifre1", "Baska.Sifre1", ct));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.KurulumAnahtarlari WHERE Tuketildi = 1", ct));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler WHERE EgitmenID = 3 AND SifreBicimi = 2", ct));

        // A student of no course of this teacher gets no code from it, and nothing changes.
        const string Unenrolled = "SELECT CONCAT(Sifre, N'|', SifreBicimi, N'|', KurtarmaKodu) FROM dbo.Ogrenci WHERE OgrenciID = 6";
        var unenrolledBefore = await database.TextAsync(Unenrolled, ct);
        await Assert.ThrowsAsync<BusinessRuleException>(() => recovery.IssueStudentCodeAsync(teacher, "Eski Öğrenci", ct)); // has a password
        await Assert.ThrowsAsync<BusinessRuleException>(() => recovery.IssueStudentCodeAsync(teacher, "Boş Şifreli", ct)); // enrolled in no course
        Assert.Equal(unenrolledBefore, await database.TextAsync(Unenrolled, ct));

        // The student's own teacher issues a code; a wrong code is refused, the right one works once.
        var code = await recovery.IssueStudentCodeAsync(teacher, "Şifresiz Öğrenci", ct);
        Assert.Matches("^[0-9A-Z]{5}-[0-9A-Z]{5}$", code.Code);
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Ogrenci WHERE KurtarmaKodu LIKE N'%' + @code + N'%'", ct, ("@code", code.Code)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => recovery.RecoverStudentAsync("Şifresiz Öğrenci", "ZZZZZ-ZZZZZ", "Yeni.Sifre1", "Yeni.Sifre1", ct));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Ogrenci WHERE OgrenciID = 4 AND SifreBicimi = 2 AND Sifre = N'' AND KurtarmaKodu IS NOT NULL", ct));

        var student = await recovery.RecoverStudentAsync("şifresiz öğrenci", code.Code.ToLowerInvariant(), "Yeni.Sifre1", "Yeni.Sifre1", ct);
        Assert.Equal(4, student.UserId);
        Assert.Equal("Şifresiz Öğrenci", student.DisplayName);
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Ogrenci WHERE OgrenciID = 4 AND SifreBicimi = 0 AND Sifre LIKE N'PBKDF2-SHA256$%' AND KurtarmaKodu IS NULL AND KurtarmaBitis IS NULL", ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => recovery.RecoverStudentAsync("Şifresiz Öğrenci", code.Code, "Baska.Sifre1", "Baska.Sifre1", ct));
        Assert.Equal(4, (await auth.SignInStudentAsync("Şifresiz Öğrenci", "Yeni.Sifre1", ct)).UserId);
        Assert.Equal([4], (await provider.GetRequiredService<IEnrollmentService>().GetMyCoursesAsync(student, ct)).Select(c => c.Id));
        Assert.Equal(2, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler", ct));
        Assert.Equal(3, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Ogrenci", ct));
    }

    [Fact]
    public async Task Initialize_LegacyDatabaseWithConflictingRows_RefusesWithoutChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.CreateAsync(ct);
        await database.ExecuteAsync(LegacySchema, ct);
        await database.ExecuteAsync(
            """
            INSERT dbo.Ogrenci (Isim, Email, Sifre) VALUES
                (N'Gizli Ad', N'ortak@example.com', N'a'),
                (N'GİZLİ AD', N'ortak@example.com', N'b'),
                (NULL, N'adsiz@example.com', N'c');
            """,
            ct);

        await using var provider = database.BuildServices();
        var error = await Assert.ThrowsAsync<DatabaseInitializationException>(
            () => provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct));

        var message = error.InnerException?.Message;
        Assert.Contains("1 students with a missing, padded or multi-line sign-in name or e-mail address", message, StringComparison.Ordinal);
        Assert.Contains("1 student sign-in names shared by more than one student", message, StringComparison.Ordinal);
        Assert.Contains("1 e-mail addresses shared by more than one student", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Gizli", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.com", message, StringComparison.Ordinal);
        var refused = Assert.IsType<LegacyUpgradeRefusedException>(error.InnerException);
        Assert.Equal(
            [
                new LegacyUpgradeIssue(LegacyUpgradeProblem.UnclearStudentIdentities, 1),
                new LegacyUpgradeIssue(LegacyUpgradeProblem.DuplicateStudentNames, 1),
                new LegacyUpgradeIssue(LegacyUpgradeProblem.DuplicateStudentEmails, 1),
            ],
            refused.Issues);
        await AssertUnchangedAsync(database, ct);
    }

    /// <summary>
    /// The teacher role of the original application is the row in Egitmenler matched by name with '=',
    /// which ignores trailing spaces. A padded or line-broken teacher name cannot be tied to one person
    /// with certainty, so the upgrade stops without changing anything instead of trimming it: trimming
    /// could hand the teacher's rights to whoever signs in with the trimmed name.
    /// </summary>
    [Fact]
    public async Task Initialize_PaddedLegacyRole_DoesNotGrantPrivileges()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.CreateAsync(ct);
        await database.ExecuteAsync(LegacySchema, ct);
        await database.ExecuteAsync(
            """
            INSERT dbo.Egitmenler (AdSoyad, UzmanlikAlani, Sifre) VALUES
                (N'Yetkili Öğretmen ', N'Matematik', N'1234'),
                (N' Başka Öğretmen', N'Fizik', N'5678'),
                (N'Satır' + NCHAR(10) + N'Sonlu', N'Kimya', N'9999');
            """,
            ct);

        await using var provider = database.BuildServices();
        var error = await Assert.ThrowsAsync<DatabaseInitializationException>(
            () => provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct));

        var message = error.InnerException?.Message;
        Assert.Contains("3 teachers with a missing, padded or multi-line sign-in name", message, StringComparison.Ordinal);
        Assert.Contains("No changes were made", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Yetkili", message, StringComparison.Ordinal);
        await AssertUnchangedAsync(database, ct);
        Assert.Equal(1, await database.ScalarAsync(
            "SELECT COUNT(*) FROM dbo.Egitmenler WHERE DATALENGTH(AdSoyad) = DATALENGTH(N'Yetkili Öğretmen ')",
            ct));

        // No session can be issued from the refused database.
        await Assert.ThrowsAnyAsync<Exception>(
            () => provider.GetRequiredService<IAuthService>().SignInTeacherAsync("Yetkili Öğretmen", "1234", ct));
    }

    /// <summary>
    /// An enrollment table keyed by more than the student and the course allows the same student in
    /// the same course twice. Renaming such a key would keep that hole open, so the upgrade stops and
    /// reports only counts.
    /// </summary>
    [Fact]
    public async Task Upgrade_ThreeColumnEnrollmentKey_RefusesWithoutChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.CreateAsync(ct);
        await database.ExecuteAsync(
            LegacySchema.Replace(
                "PRIMARY KEY (OgrenciID, DersID));",
                "KayitID INT IDENTITY(1,1) NOT NULL, PRIMARY KEY (OgrenciID, DersID, KayitID));",
                StringComparison.Ordinal),
            ct);
        await database.ExecuteAsync(LegacyData.Replace("(2, 3), (4, 4);", "(2, 3), (2, 3), (4, 4);", StringComparison.Ordinal), ct);
        const string KeyName = "SELECT name FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.OgrenciKurslar') AND type = 'PK'";
        var keyBefore = await database.TextAsync(KeyName, ct);

        await using var provider = database.BuildServices();
        var error = await Assert.ThrowsAsync<DatabaseInitializationException>(
            () => provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct));

        var message = error.InnerException?.Message;
        Assert.Contains("3 enrollment key columns", message, StringComparison.Ordinal);
        Assert.Contains("1 student and course pairs enrolled more than once", message, StringComparison.Ordinal);
        Assert.Contains("No changes were made", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Eski", message, StringComparison.Ordinal);
        Assert.Equal(
            [
                new LegacyUpgradeIssue(LegacyUpgradeProblem.EnrollmentKeyColumns, 3),
                new LegacyUpgradeIssue(LegacyUpgradeProblem.DuplicateEnrollments, 1),
            ],
            Assert.IsType<LegacyUpgradeRefusedException>(error.InnerException).Issues);
        await AssertUnchangedAsync(database, ct);
        Assert.Equal(3, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.OgrenciKurslar", ct));
        Assert.Equal(3, await database.ScalarAsync(
            "SELECT COUNT(*) FROM sys.index_columns ic JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id WHERE i.object_id = OBJECT_ID(N'dbo.OgrenciKurslar') AND i.is_primary_key = 1",
            ct));
        Assert.Equal(keyBefore, await database.TextAsync(KeyName, ct));
        Assert.StartsWith("PK__", keyBefore, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Initialize_DefaultConfiguration_DoesNotCreateDemoCredentials()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);

        await using var provider = database.BuildServices();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);

        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler", ct));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Ogrenci", ct));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Dersler", ct));
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => provider.GetRequiredService<IAuthService>().SignInTeacherAsync("Deniz Örnek", "Demo.Ogretmen1", ct));
        Assert.True(await provider.GetRequiredService<ISetupService>().IsSetupRequiredAsync(ct));
        await AssertUpgradedSchemaAsync(database, ct);
    }

    [Fact]
    public async Task Initialize_DemoRequested_AddsTheDemoAccountsOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);

        await using var provider = database.BuildServices(options: new DataAccessOptions { SeedDemoData = true });
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);

        Assert.Equal(2, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler", ct));
        Assert.Equal(3, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Dersler", ct));
        var student = await provider.GetRequiredService<IAuthService>().SignInStudentAsync("Mert Deneme", "Demo.Ogrenci1", ct);
        Assert.Equal(AccountRole.Student, student.Role);
    }

    /// <summary>
    /// Restores a backup of the original application's database (path in EGITIM_LEGACY_BACKUP, readable
    /// by the SQL Server service) and upgrades it. Only counts, identities and checksums are compared, so
    /// no personal data from the backup ends up in test output.
    /// </summary>
    [Fact]
    public async Task Initialize_RestoredLegacyBackup_KeepsAllRows()
    {
        var backup = Environment.GetEnvironmentVariable(BackupEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(backup))
        {
            Assert.Skip($"{BackupEnvironmentVariable} is not set.");
        }

        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await RestoreAsync(database, backup, ct);

        const string Counts = """
            SELECT CHECKSUM_AGG(CHECKSUM(*)) FROM (
                SELECT 'S' AS T, COUNT(*) AS C, MAX(OgrenciID) AS M FROM dbo.Ogrenci
                UNION ALL SELECT 'T', COUNT(*), MAX(EgitmenID) FROM dbo.Egitmenler
                UNION ALL SELECT 'C', COUNT(*), MAX(DersID) FROM dbo.Dersler
                UNION ALL SELECT 'N', COUNT(*), MAX(NotID) FROM dbo.Notlar
                UNION ALL SELECT 'E', COUNT(*), MAX(OgrenciID) FROM dbo.OgrenciKurslar) s
            """;
        const string Students = "SELECT CHECKSUM_AGG(CHECKSUM(OgrenciID, Isim, Email)) FROM dbo.Ogrenci";
        const string StudentPasswords = "SELECT CHECKSUM_AGG(CHECKSUM(OgrenciID, Sifre)) FROM dbo.Ogrenci WHERE LTRIM(RTRIM(Sifre)) <> N''";
        const string Teachers = "SELECT CHECKSUM_AGG(CHECKSUM(EgitmenID, AdSoyad, UzmanlikAlani, Sifre)) FROM dbo.Egitmenler";
        const string Courses = "SELECT CHECKSUM_AGG(CHECKSUM(DersID, DersAdi, EgitmenID, Konu)) FROM dbo.Dersler";
        const string Notes = "SELECT CHECKSUM_AGG(CHECKSUM(NotID, DersID, Icerik)) FROM dbo.Notlar";
        const string Enrollments = "SELECT CHECKSUM_AGG(CHECKSUM(OgrenciID, DersID)) FROM dbo.OgrenciKurslar";
        string[] checks = [Counts, Students, StudentPasswords, Teachers, Courses, Notes, Enrollments];
        var before = new List<int>();
        foreach (var check in checks)
        {
            before.Add(await database.ScalarAsync(check, ct));
        }

        var withoutPassword = await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Ogrenci WHERE Sifre IS NULL OR LTRIM(RTRIM(Sifre)) = N''", ct);
        var withPassword = await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Ogrenci WHERE LTRIM(RTRIM(Sifre)) <> N''", ct);

        await using var provider = database.BuildServices();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);

        Assert.True(await database.ScalarAsync("SELECT COUNT(*) FROM dbo.OgrenciKurslar", ct) > 0, "The backup has no enrollments.");
        for (var i = 0; i < checks.Length; i++)
        {
            Assert.Equal(before[i], await database.ScalarAsync(checks[i], ct));
        }

        Assert.Equal(withoutPassword, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Ogrenci WHERE SifreBicimi = 2", ct));
        Assert.Equal(withPassword, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Ogrenci WHERE SifreBicimi = 1", ct));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Egitmenler WHERE SifreBicimi = 0", ct));
        Assert.False(await provider.GetRequiredService<ISetupService>().IsSetupRequiredAsync(ct));
        await AssertUpgradedSchemaAsync(database, ct);
    }

    /// <summary>A refused upgrade leaves the legacy schema exactly as it was.</summary>
    private static async Task AssertUnchangedAsync(SqlServerTestDatabase database, CancellationToken ct)
    {
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM sys.tables WHERE name = N'__EFMigrationsHistory'", ct));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM sys.columns WHERE OBJECTPROPERTY(object_id, 'IsUserTable') = 1 AND name IN (N'SifreBicimi', N'BasarisizGiris', N'KilitBitis')", ct));
        Assert.Equal(4, await database.ScalarAsync("SELECT COUNT(*) FROM sys.foreign_keys WHERE name LIKE N'FK[_][_]%'", ct));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM sys.indexes WHERE OBJECTPROPERTY(object_id, 'IsUserTable') = 1 AND name LIKE N'IX[_]%'", ct));
    }

    private static async Task AssertUpgradedSchemaAsync(SqlServerTestDatabase database, CancellationToken ct)
    {
        string[] expected =
        [
            "PK_Ogrenci", "PK_Egitmenler", "PK_Dersler", "PK_Notlar", "PK_OgrenciKurslar",
            "FK_Dersler_Egitmenler", "FK_Notlar_Dersler", "FK_OgrenciKurslar_Ogrenci", "FK_OgrenciKurslar_Dersler",
            "CK_Ogrenci_BasarisizGiris", "CK_Ogrenci_SifreBicimi", "CK_Egitmenler_BasarisizGiris", "CK_Egitmenler_SifreBicimi",
            "PK_KurulumAnahtarlari",
        ];
        foreach (var name in expected)
        {
            Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM sys.objects WHERE name = @name", ct, ("@name", name)));
        }

        // The system-named keys are replaced by named ones.
        Assert.Equal(expected.Length, await database.ScalarAsync(
            "SELECT COUNT(*) FROM sys.objects WHERE type IN ('PK', 'F', 'C') AND parent_object_id <> OBJECT_ID(N'dbo.__EFMigrationsHistory')",
            ct));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM sys.default_constraints", ct));
        Assert.Equal(3, await database.ScalarAsync(
            "SELECT COUNT(*) FROM sys.indexes WHERE is_unique = 1 AND has_filter = 0 AND name IN (N'IX_Ogrenci_Isim', N'IX_Ogrenci_Email', N'IX_Egitmenler_AdSoyad')",
            ct));
        Assert.Equal(3, await database.ScalarAsync(
            "SELECT COUNT(*) FROM sys.indexes WHERE is_unique = 0 AND name IN (N'IX_Dersler_EgitmenID', N'IX_Notlar_DersID', N'IX_OgrenciKurslar_DersID')",
            ct));
        Assert.Equal(2, await database.ScalarAsync(
            "SELECT COUNT(*) FROM sys.columns WHERE name = N'Sifre' AND max_length = 512 AND is_nullable = 0",
            ct));
        Assert.Equal(2, await database.ScalarAsync(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Ogrenci') AND name IN (N'Isim', N'Email') AND is_nullable = 0",
            ct));
    }

    private static async Task RestoreAsync(SqlServerTestDatabase database, string backup, CancellationToken ct)
    {
        await SqlServerTestDatabase.ExecuteAsync(
            database.Server,
            """
            DECLARE @files TABLE (
                LogicalName nvarchar(128), PhysicalName nvarchar(260), Type char(1), FileGroupName nvarchar(128), Size numeric(20,0),
                MaxSize numeric(20,0), FileId bigint, CreateLSN numeric(25,0), DropLSN numeric(25,0), UniqueId uniqueidentifier,
                ReadOnlyLSN numeric(25,0), ReadWriteLSN numeric(25,0), BackupSizeInBytes bigint, SourceBlockSize int, FileGroupId int,
                LogGroupGUID uniqueidentifier, DifferentialBaseLSN numeric(25,0), DifferentialBaseGUID uniqueidentifier, IsReadOnly bit,
                IsPresent bit, TDEThumbprint varbinary(32), SnapshotUrl nvarchar(360));
            INSERT @files EXEC sp_executesql N'RESTORE FILELISTONLY FROM DISK = @p', N'@p nvarchar(4000)', @p = @backup;
            DECLARE @data nvarchar(128) = (SELECT LogicalName FROM @files WHERE Type = 'D');
            DECLARE @log nvarchar(128) = (SELECT LogicalName FROM @files WHERE Type = 'L');
            DECLARE @path nvarchar(400) = CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400));
            DECLARE @mdf nvarchar(500) = @path + @name + N'.mdf';
            DECLARE @ldf nvarchar(500) = @path + @name + N'_log.ldf';
            RESTORE DATABASE @name FROM DISK = @backup WITH MOVE @data TO @mdf, MOVE @log TO @ldf, RECOVERY;
            """,
            ct,
            ("@name", database.Name),
            ("@backup", backup));
    }
}
