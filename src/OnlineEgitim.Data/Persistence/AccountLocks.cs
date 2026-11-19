using Microsoft.EntityFrameworkCore;

namespace OnlineEgitim.Data.Persistence;

/// <summary>
/// Emptiness checks that the first-run setup and the demonstration data share. Inside a serializable
/// transaction on SQL Server they take update range locks, starting with the teacher table, so a setup
/// and a demo seed (or two of either) wait for each other instead of both deciding on an empty
/// database. Other providers rely on the serializable transaction alone.
/// </summary>
internal static class AccountLocks
{
    private const string Teachers = "SELECT COUNT(*) AS [Value] FROM dbo.Egitmenler WITH (UPDLOCK, HOLDLOCK)";
    private const string Students = "SELECT COUNT(*) AS [Value] FROM dbo.Ogrenci WITH (UPDLOCK, HOLDLOCK)";
    private const string Courses = "SELECT COUNT(*) AS [Value] FROM dbo.Dersler WITH (UPDLOCK, HOLDLOCK)";
    private const string SetupKeys = "SELECT * FROM dbo.KurulumAnahtarlari WITH (UPDLOCK, HOLDLOCK)";

    public static Task<bool> AnyTeacherLockedAsync(EgitimDbContext db, CancellationToken cancellationToken) =>
        AnyLockedAsync(db, Teachers, db.Teachers, cancellationToken);

    /// <summary>Whether any student exists; to be taken after <see cref="AnyTeacherLockedAsync"/>.</summary>
    public static Task<bool> AnyStudentLockedAsync(EgitimDbContext db, CancellationToken cancellationToken) =>
        AnyLockedAsync(db, Students, db.Students, cancellationToken);

    /// <summary>Whether any teacher, student or course exists; locks the three tables in this order.</summary>
    public static async Task<bool> AnyAccountOrCourseLockedAsync(EgitimDbContext db, CancellationToken cancellationToken) =>
        await AnyTeacherLockedAsync(db, cancellationToken).ConfigureAwait(false)
        | await AnyLockedAsync(db, Students, db.Students, cancellationToken).ConfigureAwait(false)
        | await AnyLockedAsync(db, Courses, db.Courses, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// The setup keys usable at <paramref name="now"/> (unused and unexpired), locked until the commit
    /// on SQL Server. Every caller holds the teacher lock already, so this lock only guards the rows
    /// against writers that do not take it, such as the issuing script.
    /// </summary>
    public static Task<List<Domain.Entities.StoredSetupKey>> UsableSetupKeysLockedAsync(EgitimDbContext db, DateTime now, CancellationToken cancellationToken)
    {
#pragma warning disable EF1002 // A constant statement, never user input.
        var keys = db.Database.IsSqlServer()
            ? db.SetupKeys.FromSqlRaw(SetupKeys)
            : db.SetupKeys;
#pragma warning restore EF1002
        return keys
            .AsNoTracking()
            .Where(k => !k.Consumed && k.ExpiresUtc > now)
            .OrderBy(k => k.Id)
            .Select(k => new Domain.Entities.StoredSetupKey(k.Id, k.KeyHash))
            .ToListAsync(cancellationToken);
    }

    private static async Task<bool> AnyLockedAsync<TEntity>(EgitimDbContext db, string lockingCount, IQueryable<TEntity> rows, CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlServer())
        {
            return await rows.AnyAsync(cancellationToken).ConfigureAwait(false);
        }

#pragma warning disable EF1002 // One of the constant statements above, never user input.
        var count = await db.Database
            .SqlQueryRaw<int>(lockingCount)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore EF1002
        return count > 0;
    }
}
