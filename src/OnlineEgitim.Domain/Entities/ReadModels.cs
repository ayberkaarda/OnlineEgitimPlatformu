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

/// <summary>A course with its teacher, as the lists show it.</summary>
public sealed record CourseSummary(int Id, string Name, int TeacherId, string TeacherName, string? Topic);

/// <summary>The public profile of a teacher.</summary>
public sealed record TeacherProfile(int Id, string FullName, string Specialty);
