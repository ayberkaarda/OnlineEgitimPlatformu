using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Business.Auditing;

/// <summary>
/// Security relevant events. An event carries only its kind and the role of the account involved: no
/// account id, name, e-mail address, password, course or note, so an audit entry cannot identify a
/// person. The entries can only be counted.
/// </summary>
public enum AuditEvent
{
    /// <summary>A sign-in succeeded.</summary>
    SignInSucceeded = 1,

    /// <summary>
    /// A sign-in failed: unknown account, wrong password, an account without a password or a locked
    /// account. The cases share one event on purpose; whoever can read the log must not learn which
    /// account exists or is locked.
    /// </summary>
    SignInFailed = 2,

    /// <summary>A plain text or weaker stored password was replaced with a current hash.</summary>
    PasswordUpgraded = 3,

    /// <summary>A new student account was created.</summary>
    StudentRegistered = 4,

    /// <summary>The first teacher account was created by the first-run setup.</summary>
    FirstTeacherCreated = 5,

    /// <summary>A teacher created a course.</summary>
    CourseCreated = 6,

    /// <summary>A teacher added a note to a course.</summary>
    NoteAdded = 7,

    /// <summary>A student joined a course.</summary>
    Enrolled = 8,

    /// <summary>
    /// An account asked for an operation of another role, or for a course, note or student list that is
    /// not its own or does not exist (both get the same answer and the same event).
    /// </summary>
    AccessDenied = 9,
}

/// <summary>Receives <see cref="AuditEvent"/>s.</summary>
public interface IAuditLog
{
    void Record(AuditEvent auditEvent, AccountRole? role);
}

public static class AuditCodes
{
    /// <summary>Stable, lower case code of <paramref name="auditEvent"/> as it is written to the log.</summary>
    public static string Of(AuditEvent auditEvent) => auditEvent switch
    {
        AuditEvent.SignInSucceeded => "auth.sign_in.succeeded",
        AuditEvent.SignInFailed => "auth.sign_in.failed",
        AuditEvent.PasswordUpgraded => "auth.password.upgraded",
        AuditEvent.StudentRegistered => "auth.student.registered",
        AuditEvent.FirstTeacherCreated => "setup.teacher.created",
        AuditEvent.CourseCreated => "course.created",
        AuditEvent.NoteAdded => "course.note.added",
        AuditEvent.Enrolled => "course.enrolled",
        AuditEvent.AccessDenied => "access.denied",
        _ => throw new ArgumentOutOfRangeException(nameof(auditEvent), auditEvent, "Unknown audit event."),
    };

    /// <summary>Lower case role name as it is written to the log.</summary>
    public static string Of(AccountRole? role) => role switch
    {
        AccountRole.Student => "student",
        AccountRole.Teacher => "teacher",
        null => "none",
        _ => "unknown",
    };
}

/// <summary>Discards every event; used when no audit sink is registered, for example in tests.</summary>
internal sealed class NullAuditLog : IAuditLog
{
    public static readonly NullAuditLog Instance = new();

    public void Record(AuditEvent auditEvent, AccountRole? role)
    {
    }
}
