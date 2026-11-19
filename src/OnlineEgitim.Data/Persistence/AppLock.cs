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
    /// <summary>SqlClient's command timeout when none is configured.</summary>
    private const int DefaultCommandTimeoutSeconds = 30;

    /// <summary>How much longer than the lock wait the lock command may run before the client gives up.</summary>
    private const int CommandTimeoutMarginSeconds = 15;

    /// <summary>
    /// Takes the lock on <paramref name="resource"/> inside the current transaction. sp_getapplock reports
    /// failures through its return code instead of raising an error, so the code is checked and nothing
    /// else runs in the transaction when the lock was not granted.
    /// </summary>
    /// <remarks>
    /// The lock command runs with a command timeout longer than <paramref name="timeoutMilliseconds"/>, so a
    /// lock that stays taken ends as a refused lock (<see cref="AppLockNotGrantedException"/>, "busy") and
    /// never as a client side command timeout that looks like a database failure.
    /// </remarks>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    /// <exception cref="AppLockNotGrantedException">The lock was not granted, for example because another operation held it for the whole wait.</exception>
    public static Task AcquireAsync(EgitimDbContext db, string resource, int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.Database.IsSqlServer()
            ? AcquireOnSqlServerAsync(db, resource, timeoutMilliseconds, cancellationToken)
            : Task.CompletedTask;
    }

    /// <summary>
    /// The command timeout (seconds) for a lock request that waits <paramref name="lockTimeoutMilliseconds"/>:
    /// the configured one when it is long enough, otherwise the wait plus a margin. Zero means no limit.
    /// </summary>
    internal static int CommandTimeoutFor(int lockTimeoutMilliseconds, int? configuredSeconds)
    {
        var current = configuredSeconds ?? DefaultCommandTimeoutSeconds;
        if (current == 0)
        {
            return 0;
        }

        var needed = ((lockTimeoutMilliseconds + 999) / 1000) + CommandTimeoutMarginSeconds;
        return Math.Max(current, needed);
    }

    /// <summary>Throws when an sp_getapplock return code means the lock was not granted (0 and 1 mean granted).</summary>
    /// <exception cref="AppLockNotGrantedException">The code is negative or missing.</exception>
    internal static void EnsureGranted(int? returnCode, string resource)
    {
        if (returnCode is >= 0)
        {
            return;
        }

        var reason = returnCode switch
        {
            -1 => "the request timed out because another operation still holds it",
            -2 => "the request was canceled",
            -3 => "the request was chosen as a deadlock victim",
            -999 => "the lock request was invalid",
            null => "the lock procedure returned no result",
            _ => "the lock procedure failed",
        };

        throw new AppLockNotGrantedException(
            $"The lock '{resource}' could not be acquired ({reason}, sp_getapplock returned "
            + $"{returnCode?.ToString(CultureInfo.InvariantCulture) ?? "null"}). No changes were made; try again.");
    }

    private static async Task AcquireOnSqlServerAsync(EgitimDbContext db, string resource, int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("An application lock is owned by a transaction; begin the transaction first.");
        }

        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var name = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = resource };
        var timeout = new SqlParameter("@timeout", SqlDbType.Int) { Value = timeoutMilliseconds };

        var configured = db.Database.GetCommandTimeout();
        db.Database.SetCommandTimeout(CommandTimeoutFor(timeoutMilliseconds, configured));
        try
        {
#pragma warning disable EF1002 // Constant statement; every value is a parameter.
            await db.Database.ExecuteSqlRawAsync(
                "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @timeout;",
                [result, name, timeout],
                cancellationToken).ConfigureAwait(false);
#pragma warning restore EF1002
        }
        finally
        {
            db.Database.SetCommandTimeout(configured);
        }

        EnsureGranted(result.Value is int code ? code : null, resource);
    }
}

/// <summary>
/// An application lock was not granted: another operation on the same record held it for the whole wait (or
/// the request was canceled). Nothing was changed; the operation can simply be tried again.
/// </summary>
public sealed class AppLockNotGrantedException : InvalidOperationException
{
    public AppLockNotGrantedException()
        : base("An application lock could not be acquired. No changes were made; try again.")
    {
    }

    public AppLockNotGrantedException(string message)
        : base(message)
    {
    }

    public AppLockNotGrantedException(string message, Exception innerException)
        : base(message, innerException)
    {
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
    internal const int TimeoutMilliseconds = 30_000;

    internal static string Resource(int courseId) => "OnlineEgitim.Course." + courseId.ToString(CultureInfo.InvariantCulture);

    public static Task AcquireAsync(EgitimDbContext db, int courseId, CancellationToken cancellationToken) =>
        AppLock.AcquireAsync(db, Resource(courseId), TimeoutMilliseconds, cancellationToken);
}
