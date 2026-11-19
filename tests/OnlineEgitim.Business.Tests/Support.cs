using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Business.Tests;

internal static class Sessions
{
    public static UserSession Student(int id = 1) => new(AccountRole.Student, id, "Mert Deneme");

    public static UserSession Teacher(int id = 1) => new(AccountRole.Teacher, id, "Deniz Örnek");

    public static UserSession Admin(int id = 1, int version = 0) => new(AccountRole.Admin, id, "Yönetici Kişi", version);
}

/// <summary>A session guard that accepts every session; the guard itself is tested on its own.</summary>
internal sealed class CurrentSessions : ISessionGuard
{
    public static readonly CurrentSessions Instance = new();

    public List<UserSession> Checked { get; } = [];

    public Task EnsureCurrentAsync(UserSession session, CancellationToken cancellationToken)
    {
        Checked.Add(session);
        return Task.CompletedTask;
    }
}

/// <summary>The real validators, as the services get them from the container.</summary>
internal static class Validators
{
    public static readonly RegistrationInputValidator Registration = new();
    public static readonly SignInInputValidator SignIn = new();
    public static readonly FirstTeacherInputValidator FirstTeacher = new();
    public static readonly NewPasswordInputValidator NewPassword = new();
    public static readonly StudentNameInputValidator StudentName = new();
    public static readonly CourseInputValidator Course = new();
    public static readonly NoteInputValidator Note = new();
    public static readonly CourseSearchValidator CourseSearch = new();
    public static readonly ChangePasswordInputValidator ChangePassword = new();
}

/// <summary>Collects the audit events instead of writing them anywhere.</summary>
internal sealed class RecordingAudit : IAuditLog
{
    public List<(AuditEvent Event, AccountRole? Role)> Events { get; } = [];

    public void Record(AuditEvent auditEvent, AccountRole? role) => Events.Add((auditEvent, role));
}
