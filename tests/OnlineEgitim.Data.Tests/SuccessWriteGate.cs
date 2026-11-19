using Microsoft.Extensions.DependencyInjection;
using OnlineEgitim.Data.Repositories;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Data.Tests;

/// <summary>
/// A deterministic interleaving point: a sign-in that has verified the password stops right before
/// it records the success, until the test releases it.
/// </summary>
internal sealed class SuccessWriteGate
{
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when a sign-in waits at the gate.</summary>
    public Task Reached => _reached.Task;

    public void Release() => _released.TrySetResult();

    internal async Task PassAsync(CancellationToken cancellationToken)
    {
        _reached.TrySetResult();
        await _released.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal static class SuccessWriteGateExtensions
{
    /// <summary>Replaces the account repository with one that waits at <paramref name="gate"/>.</summary>
    public static IServiceCollection HoldSuccessfulSignInWrites(this IServiceCollection services, SuccessWriteGate gate)
    {
        var registration = services.Single(d => d.ServiceType == typeof(IAccountRepository));
        services.Remove(registration);
        services.AddSingleton<IAccountRepository>(provider =>
            new GatedAccountRepository(ActivatorUtilities.CreateInstance<AccountRepository>(provider), gate));
        return services;
    }

    private sealed class GatedAccountRepository(IAccountRepository inner, SuccessWriteGate gate) : IAccountRepository
    {
        public Task<AccountCredentials?> FindAsync(AccountRole role, string fullName, CancellationToken cancellationToken = default) =>
            inner.FindAsync(role, fullName, cancellationToken);

        public Task RecordFailedLoginAsync(AccountRole role, int id, int maxAttempts, DateTime lockoutEnd, CancellationToken cancellationToken = default) =>
            inner.RecordFailedLoginAsync(role, id, maxAttempts, lockoutEnd, cancellationToken);

        public async Task<bool> RecordSuccessfulLoginAsync(
            AccountRole role,
            AccountCredentials verified,
            DateTime now,
            string? newPasswordHash,
            CancellationToken cancellationToken = default)
        {
            await gate.PassAsync(cancellationToken);
            return await inner.RecordSuccessfulLoginAsync(role, verified, now, newPasswordHash, cancellationToken);
        }

        public Task<bool> StudentExistsAsync(string fullName, string email, CancellationToken cancellationToken = default) =>
            inner.StudentExistsAsync(fullName, email, cancellationToken);

        public Task<int> AddStudentAsync(Student student, CancellationToken cancellationToken = default) =>
            inner.AddStudentAsync(student, cancellationToken);

        public Task<bool> AnyTeacherAsync(CancellationToken cancellationToken = default) =>
            inner.AnyTeacherAsync(cancellationToken);

        public Task<bool> AnyAccountAsync(CancellationToken cancellationToken = default) =>
            inner.AnyAccountAsync(cancellationToken);

        public Task<FirstTeacherResult> AddFirstTeacherAsync(Teacher teacher, Func<bool> authorize, CancellationToken cancellationToken = default) =>
            inner.AddFirstTeacherAsync(teacher, authorize, cancellationToken);

        public Task<TeacherProfile?> GetTeacherAsync(int id, CancellationToken cancellationToken = default) =>
            inner.GetTeacherAsync(id, cancellationToken);

        public Task<StudentRecoveryState?> FindStudentRecoveryAsync(string fullName, CancellationToken cancellationToken = default) =>
            inner.FindStudentRecoveryAsync(fullName, cancellationToken);

        public Task<bool> SetRecoveryCodeAsync(int studentId, string codeHash, DateTime expires, CancellationToken cancellationToken = default) =>
            inner.SetRecoveryCodeAsync(studentId, codeHash, expires, cancellationToken);

        public Task<bool> CompleteStudentRecoveryAsync(StudentRecoveryState verified, DateTime now, string newPasswordHash, CancellationToken cancellationToken = default) =>
            inner.CompleteStudentRecoveryAsync(verified, now, newPasswordHash, cancellationToken);

        public Task<int?> SetPasswordOfPasswordlessTeacherAsync(string fullName, string newPasswordHash, Func<bool> authorize, CancellationToken cancellationToken = default) =>
            inner.SetPasswordOfPasswordlessTeacherAsync(fullName, newPasswordHash, authorize, cancellationToken);
    }
}
