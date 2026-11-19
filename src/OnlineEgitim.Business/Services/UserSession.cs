using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Business.Services;

/// <summary>
/// The signed-in account. Only the sign-in and setup services create sessions (the constructor is
/// internal), and every service takes one and checks the role and the ownership of the data against
/// it, so the user interface cannot widen access by passing other ids.
/// </summary>
public sealed class UserSession
{
    internal UserSession(AccountRole role, int userId, string displayName)
    {
        Role = role;
        UserId = userId;
        DisplayName = displayName;
    }

    public AccountRole Role { get; }

    /// <summary>Id of the account in the table of its <see cref="Role"/>.</summary>
    public int UserId { get; }

    public string DisplayName { get; }

    public bool IsStudent => Role == AccountRole.Student;

    public bool IsTeacher => Role == AccountRole.Teacher;
}
