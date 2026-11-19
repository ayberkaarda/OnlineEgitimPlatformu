using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace OnlineEgitim.Data.Persistence;

/// <summary>
/// Exclusive SQL Server application locks owned by the current transaction and released when it ends.
/// SQLite (used by the tests) needs none: its writing transactions are serialized already.
/// </summary>
internal static class AppLock
{
    /// <summary>
    /// Takes the lock on <paramref name="resource"/> inside the current transaction. sp_getapplock reports
    /// failures through its return code instead of raising an error, so the code is checked and nothing
    /// else runs in the transaction when the lock was not granted.
    /// </summary>
    /// <exception cref="InvalidOperationException">No transaction is open, or the lock was not granted.</exception>
    public static async Task AcquireAsync(EgitimDbContext db, string resource, int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlServer())
        {
            return;
        }

        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("An application lock is owned by a transaction; begin the transaction first.");
        }

        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var name = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = resource };
        var timeout = new SqlParameter("@timeout", SqlDbType.Int) { Value = timeoutMilliseconds };
#pragma warning disable EF1002 // Constant statement; every value is a parameter.
        await db.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @timeout;",
            [result, name, timeout],
            cancellationToken).ConfigureAwait(false);
#pragma warning restore EF1002

        if (result.Value is int code && code >= 0)
        {
            return;
        }

        var returned = result.Value is int value ? value.ToString(CultureInfo.InvariantCulture) : "no result";
        throw new InvalidOperationException(
            $"The lock '{resource}' could not be acquired (sp_getapplock returned {returned}). No changes were made; try again.");
    }
}

/// <summary>
/// One lock per course that serializes every write depending on the course's seats: enrolling,
/// leaving, changing the capacity and deleting the course. Each of these transactions takes exactly
/// one course lock, as its first statement, and no other application lock, so two of them can never
/// wait on each other in opposite order. Counting the active enrollments under this lock is what keeps
/// the capacity: there is no stored counter that could drift from the rows.
/// </summary>
internal static class CourseLock
{
    private const int TimeoutMilliseconds = 30_000;

    public static Task AcquireAsync(EgitimDbContext db, int courseId, CancellationToken cancellationToken) =>
        AppLock.AcquireAsync(
            db,
            "OnlineEgitim.Course." + courseId.ToString(CultureInfo.InvariantCulture),
            TimeoutMilliseconds,
            cancellationToken);
}
