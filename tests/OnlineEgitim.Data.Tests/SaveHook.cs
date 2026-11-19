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
