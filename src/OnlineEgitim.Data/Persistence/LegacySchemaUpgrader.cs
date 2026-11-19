using System.Data;
using System.Globalization;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using OnlineEgitim.Data.Persistence.Migrations;

namespace OnlineEgitim.Data.Persistence;

/// <summary>
/// Adopts a database created by the original application (Ogrenci, Egitmenler, Dersler, Notlar and
/// OgrenciKurslar without an EF Core migrations history). The existing rows, identity values and plain
/// text passwords are kept; passwords are marked as legacy plain text and hashed on each account's next
/// successful sign-in, and accounts that never had a password (NULL or empty) are marked as having
/// none, so they stay unable to sign in as they were. The system-named keys are replaced by the named
/// keys, constraints and indexes of <see cref="InitialCreate"/>, which is then recorded as applied.
/// </summary>
/// <remarks>
/// Only exactly known values are converted (NULL passwords). Rows whose sign-in identity is unclear (a
/// missing, padded or duplicated name or e-mail address) stop the upgrade with a message that names the
/// problem and counts the rows, before anything is changed: guessing which row is meant could hand one
/// person's account, or a teacher's rights, to another. Every step checks the current schema first, and
/// the whole upgrade runs in one transaction under an application lock, so it is safe to run repeatedly
/// and from concurrent processes.
/// </remarks>
internal static class LegacySchemaUpgrader
{
    private static readonly string[] LegacyTables = ["Ogrenci", "Egitmenler", "Dersler", "Notlar", "OgrenciKurslar"];

    /// <summary>Identifier of the first migration; a database without it in its history may be legacy.</summary>
    internal static string BaselineMigrationId { get; } = MigrationId<InitialCreate>();

    /// <summary>The migrations whose schema the upgrade creates itself and records as applied.</summary>
    internal static IReadOnlyList<string> AdoptedMigrationIds { get; } = [BaselineMigrationId];

    private static string MigrationId<TMigration>()
        where TMigration : Migration =>
        typeof(TMigration).GetCustomAttribute<MigrationAttribute>()!.Id;

    /// <summary>
    /// Upgrades a legacy database in place. Returns <see langword="false"/> when there is nothing to
    /// adopt (no database yet, an empty database, or the baseline migration is already applied).
    /// </summary>
    public static async Task<bool> UpgradeIfLegacyAsync(EgitimDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.GetService<IRelationalDatabaseCreator>().ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (!await IsLegacyAsync(db, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            db,
            static async (context, token) =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(token).ConfigureAwait(false);

                await AcquireUpgradeLockAsync(context, token).ConfigureAwait(false);

                // Another process may have finished the upgrade while this one waited for the lock.
                if (!await IsLegacyAsync(context, token).ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                    return false;
                }

                await EnsureLegacyShapeAsync(context, token).ConfigureAwait(false);
                await ValidateDataAsync(context, token).ConfigureAwait(false);

                foreach (var statement in UpgradeStatements)
                {
                    await ExecuteAsync(context, statement, token).ConfigureAwait(false);
                }

                await MarkAdoptedMigrationsAppliedAsync(context, token).ConfigureAwait(false);
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return true;
            },
            verifySucceeded: null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes the transaction scoped application lock that serializes upgrades. sp_getapplock reports
    /// failures through its return code instead of raising an error, so the code is checked and the
    /// upgrade stops before any schema statement runs when the lock was not granted.
    /// </summary>
    private static async Task AcquireUpgradeLockAsync(EgitimDbContext db, CancellationToken cancellationToken)
    {
        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };

        // The command outlasts the 60 second wait, so a running upgrade ends as a refused lock, not a timeout.
        var configured = db.Database.GetCommandTimeout();
        db.Database.SetCommandTimeout(AppLock.CommandTimeoutFor(UpgradeLockTimeoutMilliseconds, configured));
        try
        {
#pragma warning disable EF1002 // Constant statement; the only parameter is the output value.
            await db.Database.ExecuteSqlRawAsync(
                "EXEC @result = sp_getapplock @Resource = N'OnlineEgitim.LegacySchemaUpgrade', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 60000;",
                [result],
                cancellationToken).ConfigureAwait(false);
#pragma warning restore EF1002
        }
        finally
        {
            db.Database.SetCommandTimeout(configured);
        }

        EnsureLockGranted(result.Value is int code ? code : null);
    }

    /// <summary>How long a start waits for another start's upgrade (the value in the statement above).</summary>
    private const int UpgradeLockTimeoutMilliseconds = 60_000;

    /// <summary>Throws when an sp_getapplock return code means the lock was not granted.</summary>
    /// <exception cref="AppLockNotGrantedException">The upgrade lock was not granted.</exception>
    internal static void EnsureLockGranted(int? returnCode) =>
        AppLock.EnsureGranted(returnCode, "OnlineEgitim.LegacySchemaUpgrade");

    private static async Task<bool> IsLegacyAsync(EgitimDbContext db, CancellationToken cancellationToken)
    {
        var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);
        if (applied.Contains(BaselineMigrationId, StringComparer.Ordinal))
        {
            return false;
        }

        return await CountExistingLegacyTablesAsync(db, cancellationToken).ConfigureAwait(false) > 0;
    }

    private static Task<int> CountExistingLegacyTablesAsync(EgitimDbContext db, CancellationToken cancellationToken) =>
        ScalarAsync(
            db,
            "SELECT COUNT(*) AS [Value] FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo') AND name IN (N'Ogrenci', N'Egitmenler', N'Dersler', N'Notlar', N'OgrenciKurslar')",
            cancellationToken);

    /// <summary>
    /// All five tables must be there, and the enrollments keyed by exactly the student and the course:
    /// a key with any other column would let the same student join the same course twice, and the
    /// upgrade only renames the key, so such a key is refused instead of adopted.
    /// </summary>
    private static async Task EnsureLegacyShapeAsync(EgitimDbContext db, CancellationToken cancellationToken)
    {
        var count = await CountExistingLegacyTablesAsync(db, cancellationToken).ConfigureAwait(false);
        if (count != LegacyTables.Length)
        {
            throw new LegacyUpgradeRefusedException(
                [new LegacyUpgradeIssue(LegacyUpgradeProblem.PartialSchema, LegacyTables.Length - count)],
                "The database contains only part of the course schema (Ogrenci, Egitmenler, Dersler, Notlar and "
                + "OgrenciKurslar keyed by OgrenciID and DersID) and no migration history, so it cannot be upgraded automatically.");
        }

        const string KeyColumns = """
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            WHERE i.object_id = OBJECT_ID(N'dbo.OgrenciKurslar') AND i.is_primary_key = 1
            """;
        var keyColumns = await ScalarAsync(db, "SELECT COUNT(*) AS [Value] " + KeyColumns, cancellationToken).ConfigureAwait(false);
        var studentAndCourse = await ScalarAsync(
            db,
            "SELECT COUNT(*) AS [Value] " + KeyColumns + " AND COL_NAME(ic.object_id, ic.column_id) IN (N'OgrenciID', N'DersID')",
            cancellationToken).ConfigureAwait(false);
        if (keyColumns == 2 && studentAndCourse == 2)
        {
            return;
        }

        var duplicates = await ScalarAsync(
            db,
            "SELECT COUNT(*) AS [Value] FROM (SELECT OgrenciID, DersID FROM dbo.OgrenciKurslar GROUP BY OgrenciID, DersID HAVING COUNT(*) > 1) d",
            cancellationToken).ConfigureAwait(false);
        IReadOnlyList<LegacyUpgradeIssue> issues = duplicates > 0
            ? [new(LegacyUpgradeProblem.EnrollmentKeyColumns, keyColumns), new(LegacyUpgradeProblem.DuplicateEnrollments, duplicates)]
            : [new(LegacyUpgradeProblem.EnrollmentKeyColumns, keyColumns)];
        throw new LegacyUpgradeRefusedException(issues, string.Create(
            CultureInfo.InvariantCulture,
            $"The enrollments of the existing course database are not keyed by exactly OgrenciID and DersID "
            + $"({keyColumns} enrollment key columns, {studentAndCourse} of them OgrenciID or DersID; "
            + $"{duplicates} student and course pairs enrolled more than once), so it cannot be upgraded automatically. No changes were made."));
    }

    // A value with leading or trailing spaces, or with a tab or line break inside. The original
    // application compared names with '=', which ignores trailing spaces, so such a name cannot be
    // matched to one account with certainty.
    private static string Unclear(string column) =>
        $"({column} IS NULL OR LTRIM(RTRIM({column})) = N'' OR DATALENGTH({column}) <> DATALENGTH(LTRIM(RTRIM({column}))) "
        + $"OR PATINDEX(N'%[' + NCHAR(9) + NCHAR(10) + NCHAR(13) + N']%', {column}) > 0)";

    /// <summary>
    /// Refuses the upgrade, before anything is changed, when existing rows cannot satisfy the new
    /// constraints or do not identify one account with certainty. Fixing such rows needs a human
    /// decision, so no data is altered or deleted here. Only counts are reported; row contents (names,
    /// e-mail addresses, passwords) never appear in the message.
    /// </summary>
    private static async Task ValidateDataAsync(EgitimDbContext db, CancellationToken cancellationToken)
    {
        (string Sql, LegacyUpgradeProblem Kind, string Problem)[] checks =
        [
            (
                $"SELECT COUNT(*) AS [Value] FROM dbo.Egitmenler WHERE {Unclear("AdSoyad")} OR UzmanlikAlani IS NULL",
                LegacyUpgradeProblem.UnclearTeacherIdentities,
                "teachers with a missing, padded or multi-line sign-in name or a missing specialty"),
            (
                "SELECT COUNT(*) AS [Value] FROM (SELECT AdSoyad FROM dbo.Egitmenler GROUP BY AdSoyad HAVING COUNT(*) > 1) d",
                LegacyUpgradeProblem.DuplicateTeacherNames,
                "teacher sign-in names shared by more than one teacher"),
            (
                $"SELECT COUNT(*) AS [Value] FROM dbo.Ogrenci WHERE {Unclear("Isim")} OR {Unclear("Email")}",
                LegacyUpgradeProblem.UnclearStudentIdentities,
                "students with a missing, padded or multi-line sign-in name or e-mail address"),
            (
                "SELECT COUNT(*) AS [Value] FROM (SELECT Isim FROM dbo.Ogrenci GROUP BY Isim HAVING COUNT(*) > 1) d",
                LegacyUpgradeProblem.DuplicateStudentNames,
                "student sign-in names shared by more than one student"),
            (
                "SELECT COUNT(*) AS [Value] FROM (SELECT Email FROM dbo.Ogrenci GROUP BY Email HAVING COUNT(*) > 1) d",
                LegacyUpgradeProblem.DuplicateStudentEmails,
                "e-mail addresses shared by more than one student"),
            (
                """
                SELECT COUNT(*) AS [Value] FROM dbo.Dersler d
                WHERE d.DersAdi IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.Egitmenler e WHERE e.EgitmenID = d.EgitmenID)
                """,
                LegacyUpgradeProblem.InvalidCourses,
                "courses without a name or without an existing teacher"),
        ];

        var problems = new List<string>();
        var issues = new List<LegacyUpgradeIssue>();
        foreach (var (sql, kind, problem) in checks)
        {
            var count = await ScalarAsync(db, sql, cancellationToken).ConfigureAwait(false);
            if (count > 0)
            {
                problems.Add($"{count} {problem}");
                issues.Add(new LegacyUpgradeIssue(kind, count));
            }
        }

        if (problems.Count > 0)
        {
            throw new LegacyUpgradeRefusedException(
                issues,
                "The existing course database cannot be upgraded until these rows are corrected: "
                + string.Join("; ", problems) + ". No changes were made.");
        }
    }

    /// <summary>
    /// Brings the legacy tables to the shape created by <see cref="InitialCreate"/>. Each statement is
    /// guarded so it only runs when the corresponding part of the schema is missing.
    /// </summary>
    private static readonly string[] UpgradeStatements =
    [
        // The system-named foreign keys are replaced by the model's named, non-cascading ones.
        DropForeignKeys("Dersler"),
        DropForeignKeys("Notlar"),
        DropForeignKeys("OgrenciKurslar"),

        RenamePrimaryKey("Ogrenci", "PK_Ogrenci"),
        RenamePrimaryKey("Egitmenler", "PK_Egitmenler"),
        RenamePrimaryKey("Dersler", "PK_Dersler"),
        RenamePrimaryKey("Notlar", "PK_Notlar"),
        RenamePrimaryKey("OgrenciKurslar", "PK_OgrenciKurslar"),

        // Existing accounts get the values of a fresh account. Every stored password of the original
        // application is plain text (format 1), whatever it looks like; an account without one
        // (NULL or empty: it could never sign in) gets format 2 and an empty value.
        PasswordColumns("Ogrenci"),
        PasswordColumns("Egitmenler"),

        AlterColumn("Ogrenci", "Isim", "nvarchar", 200, "NVARCHAR(100) NOT NULL"),
        AlterColumn("Ogrenci", "Email", "nvarchar", 200, "NVARCHAR(100) NOT NULL"),
        AlterColumn("Ogrenci", "Sifre", "nvarchar", 512, "NVARCHAR(256) NOT NULL"),
        AlterColumn("Egitmenler", "AdSoyad", "nvarchar", 200, "NVARCHAR(100) NOT NULL"),
        AlterColumn("Egitmenler", "UzmanlikAlani", "nvarchar", 200, "NVARCHAR(100) NOT NULL"),
        AlterColumn("Egitmenler", "Sifre", "nvarchar", 512, "NVARCHAR(256) NOT NULL"),
        AlterColumn("Dersler", "DersAdi", "nvarchar", 200, "NVARCHAR(100) NOT NULL"),
        AlterColumn("Dersler", "Konu", "nvarchar", 400, "NVARCHAR(200) NULL", nullable: true),
        AlterColumn("Notlar", "Icerik", "nvarchar", 1000, "NVARCHAR(500) NOT NULL"),

        AddColumnWithInitialValue("Ogrenci", "BasarisizGiris", "INT NOT NULL", "0"),
        AddColumn("Ogrenci", "KilitBitis", "DATETIME2 NULL"),
        AddColumnWithInitialValue("Egitmenler", "BasarisizGiris", "INT NOT NULL", "0"),
        AddColumn("Egitmenler", "KilitBitis", "DATETIME2 NULL"),

        AddForeignKey("Dersler", "FK_Dersler_Egitmenler", "EgitmenID", "Egitmenler", "EgitmenID"),
        AddForeignKey("Notlar", "FK_Notlar_Dersler", "DersID", "Dersler", "DersID"),
        AddForeignKey("OgrenciKurslar", "FK_OgrenciKurslar_Ogrenci", "OgrenciID", "Ogrenci", "OgrenciID"),
        AddForeignKey("OgrenciKurslar", "FK_OgrenciKurslar_Dersler", "DersID", "Dersler", "DersID"),

        AddCheck("Ogrenci", "CK_Ogrenci_BasarisizGiris", SchemaLimits.FailedLoginCheck),
        AddCheck("Ogrenci", "CK_Ogrenci_SifreBicimi", SchemaLimits.PasswordFormatCheck),
        AddCheck("Egitmenler", "CK_Egitmenler_BasarisizGiris", SchemaLimits.FailedLoginCheck),
        AddCheck("Egitmenler", "CK_Egitmenler_SifreBicimi", SchemaLimits.PasswordFormatCheck),

        CreateIndex("Ogrenci", "IX_Ogrenci_Isim", "CREATE UNIQUE INDEX IX_Ogrenci_Isim ON dbo.Ogrenci (Isim)"),
        CreateIndex("Ogrenci", "IX_Ogrenci_Email", "CREATE UNIQUE INDEX IX_Ogrenci_Email ON dbo.Ogrenci (Email)"),
        CreateIndex("Egitmenler", "IX_Egitmenler_AdSoyad", "CREATE UNIQUE INDEX IX_Egitmenler_AdSoyad ON dbo.Egitmenler (AdSoyad)"),
        CreateIndex("Dersler", "IX_Dersler_EgitmenID", "CREATE INDEX IX_Dersler_EgitmenID ON dbo.Dersler (EgitmenID)"),
        CreateIndex("Notlar", "IX_Notlar_DersID", "CREATE INDEX IX_Notlar_DersID ON dbo.Notlar (DersID)"),
        CreateIndex("OgrenciKurslar", "IX_OgrenciKurslar_DersID", "CREATE INDEX IX_OgrenciKurslar_DersID ON dbo.OgrenciKurslar (DersID)"),
    ];

    /// <summary>
    /// Adds the password format column (1 for every stored password, 2 where none was stored) and
    /// replaces NULL passwords with an empty value. The column only exists once this step has run, so
    /// the step cannot be repeated.
    /// </summary>
    private static string PasswordColumns(string table) =>
        $"""
        IF COL_LENGTH(N'dbo.{table}', N'SifreBicimi') IS NULL
        BEGIN
            EXEC(N'ALTER TABLE dbo.{table} ADD SifreBicimi INT NOT NULL CONSTRAINT DF_Upgrade_{table}_SifreBicimi DEFAULT 1 WITH VALUES');
            EXEC(N'ALTER TABLE dbo.{table} DROP CONSTRAINT DF_Upgrade_{table}_SifreBicimi');
            EXEC(N'UPDATE dbo.{table} SET SifreBicimi = 2, Sifre = N'''' WHERE Sifre IS NULL OR LTRIM(RTRIM(Sifre)) = N''''');
        END
        """;

    private static string AlterColumn(string table, string column, string typeName, int maxLength, string definition, bool nullable = false) =>
        $"""
        IF EXISTS (
            SELECT 1 FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID(N'dbo.{table}') AND c.name = N'{column}'
              AND (c.is_nullable <> {(nullable ? 1 : 0)} OR t.name <> N'{typeName}' OR c.max_length <> {maxLength}))
            EXEC(N'ALTER TABLE dbo.{table} ALTER COLUMN {column} {definition}');
        """;

    private static string AddColumn(string table, string column, string definition) =>
        $"""
        IF COL_LENGTH(N'dbo.{table}', N'{column}') IS NULL
            EXEC(N'ALTER TABLE dbo.{table} ADD {column} {definition}');
        """;

    /// <summary>
    /// Adds a NOT NULL column filled with <paramref name="value"/> for the existing rows. The default
    /// constraint only exists for the duration of the statement, as the migration creates none.
    /// </summary>
    private static string AddColumnWithInitialValue(string table, string column, string definition, string value) =>
        $"""
        IF COL_LENGTH(N'dbo.{table}', N'{column}') IS NULL
        BEGIN
            EXEC(N'ALTER TABLE dbo.{table} ADD {column} {definition} CONSTRAINT DF_Upgrade_{table}_{column} DEFAULT {value} WITH VALUES');
            EXEC(N'ALTER TABLE dbo.{table} DROP CONSTRAINT DF_Upgrade_{table}_{column}');
        END
        """;

    private static string RenamePrimaryKey(string table, string name) =>
        $"""
        DECLARE @current sysname = (
            SELECT name FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.{table}') AND type = 'PK');
        IF @current IS NOT NULL AND @current <> N'{name}'
        BEGIN
            DECLARE @qualified nvarchar(300) = N'dbo.' + QUOTENAME(@current);
            EXEC sp_rename @objname = @qualified, @newname = N'{name}', @objtype = N'OBJECT';
        END
        """;

    /// <summary>Drops the foreign keys of <paramref name="table"/> that do not carry a name of the model.</summary>
    private static string DropForeignKeys(string table) =>
        $"""
        DECLARE @drop nvarchar(max) = N'';
        SELECT @drop += N'ALTER TABLE dbo.{table} DROP CONSTRAINT ' + QUOTENAME(f.name) + N';'
        FROM sys.foreign_keys f
        WHERE f.parent_object_id = OBJECT_ID(N'dbo.{table}') AND f.name NOT LIKE N'FK[_]{table}[_]%';
        IF @drop <> N'' EXEC(@drop);
        """;

    /// <summary>Adds the named, non-cascading foreign key when it is missing.</summary>
    private static string AddForeignKey(string table, string name, string column, string principal, string principalColumn) =>
        $"""
        IF OBJECT_ID(N'dbo.{name}', N'F') IS NULL
            EXEC(N'ALTER TABLE dbo.{table} WITH CHECK ADD CONSTRAINT {name} FOREIGN KEY ({column}) REFERENCES dbo.{principal} ({principalColumn})');
        """;

    private static string AddCheck(string table, string name, string condition) =>
        $"""
        IF OBJECT_ID(N'dbo.{name}', N'C') IS NULL
            EXEC(N'ALTER TABLE dbo.{table} WITH CHECK ADD CONSTRAINT {name} CHECK ({condition.Replace("'", "''", StringComparison.Ordinal)})');
        """;

    private static string CreateIndex(string table, string name, string createStatement) =>
        $"""
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.{table}') AND name = N'{name}')
            EXEC(N'{createStatement.Replace("'", "''", StringComparison.Ordinal)}');
        """;

    private static async Task MarkAdoptedMigrationsAppliedAsync(EgitimDbContext db, CancellationToken cancellationToken)
    {
        var history = db.GetService<IHistoryRepository>();
        await ExecuteAsync(db, history.GetCreateIfNotExistsScript(), cancellationToken).ConfigureAwait(false);
        foreach (var migrationId in AdoptedMigrationIds)
        {
            await ExecuteAsync(
                db,
                history.GetInsertScript(new HistoryRow(migrationId, ProductInfo.GetVersion())),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static Task<int> ScalarAsync(EgitimDbContext db, string sql, CancellationToken cancellationToken) =>
#pragma warning disable EF1002 // The statements are built from constants in this class, never from user input.
        db.Database.SqlQueryRaw<int>(sql).SingleAsync(cancellationToken);
#pragma warning restore EF1002

    private static Task<int> ExecuteAsync(EgitimDbContext db, string sql, CancellationToken cancellationToken) =>
#pragma warning disable EF1002 // The statements are built from constants in this class, never from user input.
        db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
#pragma warning restore EF1002
}
