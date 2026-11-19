using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace OnlineEgitim.Data.Tests;

/// <summary>
/// A deterministic interleaving point in a context's writes: before the first SaveChanges whose added
/// entities match <see cref="Matches"/>, <see cref="BeforeSave"/> runs once (it may wait or throw).
/// </summary>
internal sealed class SaveHook(Func<object, bool> matches, Func<CancellationToken, Task> beforeSave) : SaveChangesInterceptor
{
    private int _fired;

    public Func<object, bool> Matches { get; } = matches;

    public bool Fired => Volatile.Read(ref _fired) == 1;

    /// <summary>Fails the first matching save with <see cref="InjectedFailureException"/>.</summary>
    public static SaveHook FailOnce(Func<object, bool> matches) =>
        new(matches, _ => throw new InjectedFailureException());

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context
            && context.ChangeTracker.Entries().Any(e => e.State == EntityState.Added && Matches(e.Entity))
            && Interlocked.Exchange(ref _fired, 1) == 0)
        {
            await beforeSave(cancellationToken);
        }

        return result;
    }

}

/// <summary>
/// A deterministic failure of the next transaction commit after <see cref="Arm"/>: everything the
/// transaction wrote, including the use of a setup key, has to roll back.
/// </summary>
internal sealed class CommitFailure : DbTransactionInterceptor
{
    private int _armed;
    private int _fired;

    public bool Fired => Volatile.Read(ref _fired) == 1;

    public void Arm() => Volatile.Write(ref _armed, 1);

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(
        System.Data.Common.DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _armed, 0) == 1)
        {
            Volatile.Write(ref _fired, 1);
            throw new InjectedFailureException();
        }

        return ValueTask.FromResult(result);
    }
}

/// <summary>
/// A deterministic failure of the next command after <see cref="Arm"/> whose text contains all of
/// the given fragments, for example the account write of a setup that has already used its key.
/// </summary>
internal sealed class CommandFailure(params string[] fragments) : DbCommandInterceptor
{
    private int _armed;
    private int _fired;

    public bool Fired => Volatile.Read(ref _fired) == 1;

    public void Arm() => Volatile.Write(ref _armed, 1);

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        System.Data.Common.DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        FailIfMatching(command);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
        System.Data.Common.DbCommand command,
        CommandEventData eventData,
        InterceptionResult<System.Data.Common.DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        FailIfMatching(command);
        return ValueTask.FromResult(result);
    }

    private void FailIfMatching(System.Data.Common.DbCommand command)
    {
        if (Volatile.Read(ref _armed) == 1
            && fragments.All(f => command.CommandText.Contains(f, StringComparison.OrdinalIgnoreCase))
            && Interlocked.Exchange(ref _armed, 0) == 1)
        {
            Volatile.Write(ref _fired, 1);
            throw new InjectedFailureException();
        }
    }
}

/// <summary>The controlled failure of a test.</summary>
internal sealed class InjectedFailureException : Exception
{
    public InjectedFailureException()
        : base("Injected by the test.")
    {
    }

    public InjectedFailureException(string message)
        : base(message)
    {
    }

    public InjectedFailureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
