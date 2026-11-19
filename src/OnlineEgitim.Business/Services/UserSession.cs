using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Business.Services;

/// <summary>
/// The signed-in account. Only the sign-in, setup and recovery services create sessions (the
/// constructor is internal), and every service takes one and checks the role and the ownership of the
/// data against it, so the user interface cannot widen access by passing other ids. A session also
/// carries the account's session version: once a password is reset, the account disabled or its role
/// changed, the stored version moves on and the session is refused.
/// </summary>
public sealed class UserSession
{
    internal UserSession(AccountRole role, int userId, string displayName, int sessionVersion = 0)
    {
        Role = role;
        UserId = userId;
        DisplayName = displayName;
        SessionVersion = sessionVersion;
    }

    public AccountRole Role { get; }

    /// <summary>Id of the account in the table of its <see cref="Role"/>.</summary>
    public int UserId { get; }

    public string DisplayName { get; }

    /// <summary>The account's session version when the session was issued.</summary>
    public int SessionVersion { get; }

    public bool IsStudent => Role == AccountRole.Student;

    public bool IsTeacher => Role == AccountRole.Teacher;

    public bool IsAdmin => Role == AccountRole.Admin;
}
