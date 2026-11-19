namespace OnlineEgitim.Domain.Entities;

/// <summary>What a sign-in needs to know about an account.</summary>
public sealed record AccountCredentials(
    int Id,
    string FullName,
    string PasswordHash,
    PasswordFormat PasswordFormat,
    int FailedLoginCount,
    DateTime? LockoutEnd);

/// <summary>What the recovery of a student account without a password needs to know.</summary>
public sealed record StudentRecoveryState(
    int Id,
    string FullName,
    PasswordFormat PasswordFormat,
    string? RecoveryCodeHash,
    DateTime? RecoveryCodeExpires,
    DateTime? LockoutEnd);

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

/// <summary>The public profile of a teacher.</summary>
public sealed record TeacherProfile(int Id, string FullName, string Specialty);
