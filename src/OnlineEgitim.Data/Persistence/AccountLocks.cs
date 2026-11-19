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

    public static Task<bool> AnyTeacherLockedAsync(EgitimDbContext db, CancellationToken cancellationToken) =>
        AnyLockedAsync(db, Teachers, db.Teachers, cancellationToken);

    /// <summary>Whether any teacher, student or course exists; locks the three tables in this order.</summary>
    public static async Task<bool> AnyAccountOrCourseLockedAsync(EgitimDbContext db, CancellationToken cancellationToken) =>
        await AnyTeacherLockedAsync(db, cancellationToken).ConfigureAwait(false)
        | await AnyLockedAsync(db, Students, db.Students, cancellationToken).ConfigureAwait(false)
        | await AnyLockedAsync(db, Courses, db.Courses, cancellationToken).ConfigureAwait(false);

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
