using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Domain.Repositories;

/// <summary>Student and teacher accounts and their sign-in state.</summary>
public interface IAccountRepository
{
    /// <summary>The account of <paramref name="role"/> that signs in with <paramref name="fullName"/>.</summary>
    Task<AccountCredentials?> FindAsync(AccountRole role, string fullName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts a failed sign-in atomically. When the count reaches <paramref name="maxAttempts"/> the
    /// account is locked until <paramref name="lockoutEnd"/> and the count starts again from zero.
    /// </summary>
    Task RecordFailedLoginAsync(AccountRole role, int id, int maxAttempts, DateTime lockoutEnd, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a successful sign-in in one conditional UPDATE: only while the account is not locked at
    /// <paramref name="now"/> and still has the password value and format of <paramref name="verified"/>
    /// are the failed attempts reset. When <paramref name="newPasswordHash"/> is given the stored
    /// password is replaced with it and its format becomes <see cref="PasswordFormat.Pbkdf2"/> in the
    /// same statement (upgrade of a plain text or weaker hash).
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the account changed after it was read (for example a concurrent
    /// failure locked it); nothing is written then and the sign-in must be refused.
    /// </returns>
    Task<bool> RecordSuccessfulLoginAsync(
        AccountRole role,
        AccountCredentials verified,
        DateTime now,
        string? newPasswordHash,
        CancellationToken cancellationToken = default);

    /// <summary>Whether a student signs in with <paramref name="fullName"/> or uses <paramref name="email"/>.</summary>
    Task<bool> StudentExistsAsync(string fullName, string email, CancellationToken cancellationToken = default);

    /// <summary>Stores a new student and returns its id.</summary>
    /// <exception cref="DuplicateAccountException">The name or the e-mail address is taken.</exception>
    Task<int> AddStudentAsync(Student student, CancellationToken cancellationToken = default);

    Task<bool> AnyTeacherAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether any student or teacher account exists.</summary>
    Task<bool> AnyAccountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores <paramref name="teacher"/> only if no teacher exists yet, atomically: of concurrent calls
    /// at most one succeeds. <paramref name="authorize"/> is the final decision about the caller's
    /// right to create the first teacher: it is called inside the write transaction, after the check
    /// found no teacher and while concurrent setups wait, and nothing is written when it returns
    /// <see langword="false"/>.
    /// </summary>
    Task<FirstTeacherResult> AddFirstTeacherAsync(Teacher teacher, Func<bool> authorize, CancellationToken cancellationToken = default);

    Task<TeacherProfile?> GetTeacherAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>The recovery state of the student who signs in with <paramref name="fullName"/>.</summary>
    Task<StudentRecoveryState?> FindStudentRecoveryAsync(string fullName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a recovery code hash for the student who signs in with <paramref name="studentName"/>,
    /// replacing an earlier one, in one conditional UPDATE: only while the account has no password
    /// (<see cref="PasswordFormat.None"/>) and is enrolled in at least one course of teacher
    /// <paramref name="teacherId"/>.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when no such student exists, it has a password or it is not a student
    /// of the teacher; nothing is written then.
    /// </returns>
    Task<bool> SetRecoveryCodeAsync(int teacherId, string studentName, string codeHash, DateTime expires, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives the student of <paramref name="verified"/> its first password in one conditional UPDATE:
    /// only while the account still has no password, the verified code and an unexpired code at
    /// <paramref name="now"/>, and is not locked. The PBKDF2 hash and its format are written together
    /// and the code is removed, so it works once.
    /// </summary>
    /// <returns><see langword="false"/> when nothing was written.</returns>
    Task<bool> CompleteStudentRecoveryAsync(StudentRecoveryState verified, DateTime now, string newPasswordHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives the teacher who signs in with <paramref name="fullName"/> and has no password its first
    /// password (a PBKDF2 hash, written together with its format). <paramref name="authorize"/> is the
    /// final decision: it is called inside the transaction after the account was matched and nothing
    /// is written when it returns <see langword="false"/>.
    /// </summary>
    /// <returns>The teacher's id, or <see langword="null"/> when nothing was written.</returns>
    Task<int?> SetPasswordOfPasswordlessTeacherAsync(string fullName, string newPasswordHash, Func<bool> authorize, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of <see cref="IAccountRepository.AddFirstTeacherAsync"/>.</summary>
public enum FirstTeacherStatus
{
    /// <summary>The teacher was stored; its id is in <see cref="FirstTeacherResult.TeacherId"/>.</summary>
    Created = 0,

    /// <summary>A teacher existed already; nothing was written.</summary>
    TeacherExists = 1,

    /// <summary>The authorization was refused; nothing was written.</summary>
    NotAuthorized = 2,
}

/// <summary>Result of an attempt to store the first teacher.</summary>
public readonly record struct FirstTeacherResult(FirstTeacherStatus Status, int TeacherId);

/// <summary>Raised when a sign-in name or e-mail address is already used by another account.</summary>
public sealed class DuplicateAccountException : Exception
{
    public DuplicateAccountException()
        : base("The account name or e-mail address is already registered.")
    {
    }

    public DuplicateAccountException(string message)
        : base(message)
    {
    }

    public DuplicateAccountException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
