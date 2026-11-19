namespace OnlineEgitim.Domain.Entities;

/// <summary>
/// What a sign-in needs to know about an account. <see cref="Role"/> is the account's own role: a row
/// of the staff table is a teacher or an administrator.
/// </summary>
public sealed record AccountCredentials(
    int Id,
    string FullName,
    string PasswordHash,
    PasswordFormat PasswordFormat,
    int FailedLoginCount,
    DateTime? LockoutEnd,
    AccountRole Role = AccountRole.Student,
    bool Disabled = false,
    int SessionVersion = 0);

/// <summary>What setting a password with a single use code needs to know about an account.</summary>
public sealed record AccountRecoveryState(
    int Id,
    string FullName,
    PasswordFormat PasswordFormat,
    string? RecoveryCodeHash,
    DateTime? RecoveryCodeExpires,
    DateTime? LockoutEnd,
    AccountRole Role = AccountRole.Student,
    bool Disabled = false);

/// <summary>What decides whether a session of an account is still valid.</summary>
public sealed record SessionState(AccountRole Role, bool Disabled, int SessionVersion);

/// <summary>
/// An account as the administrators' list shows it: never a password, a hash or a code.
/// <see cref="Detail"/> is a student's e-mail address or a teacher's specialty.
/// </summary>
public sealed record AccountListing(AccountRole Role, int Id, string FullName, string Detail, bool Disabled, bool HasPassword);

/// <summary>An administrator's search: <see cref="Text"/> matches names and details, <see cref="Role"/> limits the kind.</summary>
public sealed record AccountSearchCriteria(string? Text, AccountRole? Role, int Skip, int Take);

/// <summary>One page of an account search and the number of all matching accounts.</summary>
public sealed record AccountPage(IReadOnlyList<AccountListing> Items, int TotalCount);

/// <summary>A usable setup key as a given key is compared with it.</summary>
public sealed record StoredSetupKey(int Id, byte[] KeyHash);

/// <summary>
/// A course with its teacher, as the lists show it: its capacity (<see langword="null"/> for no limit)
/// and the number of students with an active enrollment.
/// </summary>
public sealed record CourseSummary(
    int Id,
    string Name,
    int TeacherId,
    string TeacherName,
    string? Topic,
    int? Capacity = null,
    int StudentCount = 0)
{
    /// <summary>Whether a new student can still join (no limit, or fewer students than seats).</summary>
    public bool HasFreeSeat => Capacity is not { } capacity || StudentCount < capacity;
}

/// <summary>Which courses a catalogue search returns with respect to the searching student.</summary>
public enum EnrollmentFilter
{
    /// <summary>Every course.</summary>
    Any = 0,

    /// <summary>Only the courses the student is in.</summary>
    Enrolled = 1,

    /// <summary>Only the courses the student is not in.</summary>
    NotEnrolled = 2,
}

/// <summary>
/// A catalogue search for <see cref="StudentId"/>. Empty texts do not filter; the others match anywhere
/// in the course name, topic or teacher name. <see cref="Skip"/> and <see cref="Take"/> select the page.
/// </summary>
public sealed record CourseSearchCriteria(
    int StudentId,
    string? Name,
    string? Topic,
    string? TeacherName,
    bool OnlyWithFreeSeats,
    EnrollmentFilter Enrollment,
    int Skip,
    int Take);

/// <summary>A course in the catalogue, with whether the searching student is in it.</summary>
public sealed record CatalogEntry(CourseSummary Course, bool Enrolled);

/// <summary>One page of a catalogue search and the number of all matching courses.</summary>
public sealed record CatalogPage(IReadOnlyList<CatalogEntry> Items, int TotalCount);

/// <summary>The public profile of a teacher or an administrator.</summary>
public sealed record TeacherProfile(int Id, string FullName, string Specialty, AccountRole Role = AccountRole.Teacher);
