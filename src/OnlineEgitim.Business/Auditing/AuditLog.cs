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

    /// <summary>
    /// The first teacher account was created by the first-run setup. No longer recorded: the setup
    /// creates the first administrator (<see cref="FirstAdminCreated"/>); kept so the value keeps its meaning.
    /// </summary>
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

    /// <summary>A founder operation was refused: the setup key was missing, wrong, expired or used.</summary>
    SetupRefused = 10,

    /// <summary>A teacher (for a student of their course) or an administrator (for any account) issued a single use code.</summary>
    RecoveryCodeIssued = 11,

    /// <summary>An account set its password with a single use code or the setup key; its sessions ended.</summary>
    AccountRecovered = 12,

    /// <summary>A recovery was refused (wrong, used or expired code or key, or no such account).</summary>
    RecoveryFailed = 13,

    /// <summary>A registration used a name or e-mail address that is taken; no account was created.</summary>
    RegistrationNotCompleted = 14,

    /// <summary>A teacher changed the name, topic or capacity of a course.</summary>
    CourseUpdated = 15,

    /// <summary>A teacher deleted a course; its active enrollments were closed.</summary>
    CourseDeleted = 16,

    /// <summary>A student left a course.</summary>
    EnrollmentLeft = 17,

    /// <summary>An administrator created a teacher or student account (the role is the new account's).</summary>
    AccountCreated = 18,

    /// <summary>An administrator disabled an account (the role is the disabled account's).</summary>
    AccountDisabled = 19,

    /// <summary>An administrator enabled an account again (the role is the enabled account's).</summary>
    AccountEnabled = 20,

    /// <summary>An administrator made another administrator, or themselves, a teacher.</summary>
    AdminDemoted = 21,

    /// <summary>A session was refused: the account was disabled, its password reset or its role changed since.</summary>
    SessionRejected = 22,

    /// <summary>The first administrator account was created by the first-run setup.</summary>
    FirstAdminCreated = 23,

    /// <summary>A signed-in account changed its own password; its sessions ended.</summary>
    PasswordChanged = 24,

    /// <summary>A password change was refused: the current password was wrong, or the account locked or changed.</summary>
    PasswordChangeFailed = 25,
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
        AuditEvent.SetupRefused => "setup.refused",
        AuditEvent.RecoveryCodeIssued => "recovery.code.issued",
        AuditEvent.AccountRecovered => "recovery.completed",
        AuditEvent.RecoveryFailed => "recovery.failed",
        AuditEvent.RegistrationNotCompleted => "auth.student.registration_not_completed",
        AuditEvent.CourseUpdated => "course.updated",
        AuditEvent.CourseDeleted => "course.deleted",
        AuditEvent.EnrollmentLeft => "course.left",
        AuditEvent.AccountCreated => "admin.account.created",
        AuditEvent.AccountDisabled => "admin.account.disabled",
        AuditEvent.AccountEnabled => "admin.account.enabled",
        AuditEvent.AdminDemoted => "admin.role.demoted",
        AuditEvent.SessionRejected => "auth.session.rejected",
        AuditEvent.FirstAdminCreated => "setup.admin.created",
        AuditEvent.PasswordChanged => "auth.password.changed",
        AuditEvent.PasswordChangeFailed => "auth.password.change_failed",
        _ => throw new ArgumentOutOfRangeException(nameof(auditEvent), auditEvent, "Unknown audit event."),
    };

    /// <summary>
    /// The code written to a log that the local Windows user can read. It is <see cref="Of(AuditEvent)"/>
    /// except for the two outcomes of a registration: a new account and a taken name or e-mail address
    /// share one neutral code, because whoever made the attempt knows its time and could otherwise tell
    /// from the file whether someone else's name or address is registered.
    /// </summary>
    public static string ForPublicLog(AuditEvent auditEvent) => auditEvent switch
    {
        AuditEvent.StudentRegistered or AuditEvent.RegistrationNotCompleted => RegistrationSubmitted,
        _ => Of(auditEvent),
    };

    /// <summary>The neutral code of <see cref="ForPublicLog"/> for both registration outcomes.</summary>
    public const string RegistrationSubmitted = "auth.student.registration_submitted";

    /// <summary>Lower case role name as it is written to the log.</summary>
    public static string Of(AccountRole? role) => role switch
    {
        AccountRole.Student => "student",
        AccountRole.Teacher => "teacher",
        AccountRole.Admin => "admin",
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
