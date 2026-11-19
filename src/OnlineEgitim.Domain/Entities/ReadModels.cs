namespace OnlineEgitim.Domain.Entities;

/// <summary>What a sign-in needs to know about an account.</summary>
public sealed record AccountCredentials(
    int Id,
    string FullName,
    string PasswordHash,
    PasswordFormat PasswordFormat,
    int FailedLoginCount,
    DateTime? LockoutEnd);

/// <summary>A course with its teacher, as the lists show it.</summary>
public sealed record CourseSummary(int Id, string Name, int TeacherId, string TeacherName, string? Topic);

/// <summary>The public profile of a teacher.</summary>
public sealed record TeacherProfile(int Id, string FullName, string Specialty);
