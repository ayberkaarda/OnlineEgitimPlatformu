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

    /// <summary>Where the setup of the database stands; see <see cref="SetupStage"/>.</summary>
    Task<SetupStage> GetSetupStageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores <paramref name="teacher"/> only if no teacher exists yet, atomically: of concurrent calls
    /// at most one succeeds. An empty database needs no key. A database with students needs
    /// <paramref name="setupKey"/>: the key is checked and used up inside the write transaction, after
    /// the check found no teacher and while concurrent setups wait, with a conditional UPDATE. When the
    /// write fails the key stays unused; a wrong key writes nothing. There is no way to create a key
    /// here: keys come only from the database owner's script.
    /// </summary>
    Task<FirstTeacherResult> AddFirstTeacherAsync(Teacher teacher, SetupKeyAttempt? setupKey, CancellationToken cancellationToken = default);

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
    /// Gives the account of <paramref name="role"/> that signs in with <paramref name="fullName"/> and
    /// has no password its first password (a PBKDF2 hash, written together with its format), with a
    /// setup key. One serializable transaction holds the teacher table: refused while a teacher can
    /// sign in with a password; otherwise the key is checked first (before the name is looked at),
    /// then the account is written and the key used up by a conditional UPDATE. A student qualifies
    /// only while enrolled in no course (an enrolled student gets a recovery code from the course's
    /// teacher). When no such account exists or the write fails, the key stays unused.
    /// </summary>
    Task<SetupKeyWriteResult> SetFirstPasswordWithSetupKeyAsync(
        AccountRole role,
        string fullName,
        string newPasswordHash,
        SetupKeyAttempt setupKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A setup key given by someone, checked by a repository inside its write transaction. The repository
/// passes every key usable at <see cref="Now"/> (unused and unexpired); <see cref="FindMatch"/> compares
/// the given key with each of them in constant time and returns the id of the matching one, or
/// <see langword="null"/>. Wrong keys are never counted against a stored key, so nobody can make the
/// founder's key unusable by guessing.
/// </summary>
public sealed class SetupKeyAttempt
{
    private readonly Func<IReadOnlyList<StoredSetupKey>, int?> _findMatch;

    public SetupKeyAttempt(Func<IReadOnlyList<StoredSetupKey>, int?> findMatch, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(findMatch);

        _findMatch = findMatch;
        Now = now;
    }

    /// <summary>
    /// UTC time the attempt was made; selects the keys that were usable then. On SQL Server the final
    /// UPDATE that uses a key up checks its expiry again against the database's clock, because the
    /// transaction may have waited for locks since this time was taken.
    /// </summary>
    public DateTime Now { get; }

    public int? FindMatch(IReadOnlyList<StoredSetupKey> usableKeys) => _findMatch(usableKeys);
}

/// <summary>Outcome of <see cref="IAccountRepository.SetFirstPasswordWithSetupKeyAsync"/>.</summary>
public enum SetupKeyWriteStatus
{
    /// <summary>The password was set and the key used up; the id is in <see cref="SetupKeyWriteResult.AccountId"/>.</summary>
    Done = 0,

    /// <summary>The key was wrong, expired or used; nothing was written.</summary>
    NotAuthorized = 1,

    /// <summary>No account without a password has that name; nothing was written and the key stays unused.</summary>
    NoSuchAccount = 2,

    /// <summary>A teacher can sign in with a password, so no setup key is accepted; nothing was written.</summary>
    SetupCompleted = 3,
}

/// <summary>Result of a write authorized by a setup key.</summary>
public readonly record struct SetupKeyWriteResult(SetupKeyWriteStatus Status, int AccountId);

/// <summary>Outcome of <see cref="IAccountRepository.AddFirstTeacherAsync"/>.</summary>
public enum FirstTeacherStatus
{
    /// <summary>The teacher was stored; its id is in <see cref="FirstTeacherResult.TeacherId"/>.</summary>
    Created = 0,

    /// <summary>A teacher existed already; nothing was written.</summary>
    TeacherExists = 1,

    /// <summary>The setup key was wrong, expired or used; nothing was written.</summary>
    NotAuthorized = 2,

    /// <summary>The database has accounts, so a setup key is needed and none was given; nothing was written.</summary>
    KeyRequired = 3,
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
