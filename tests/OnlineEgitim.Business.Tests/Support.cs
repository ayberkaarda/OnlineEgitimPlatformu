using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Business.Tests;

internal static class Sessions
{
    public static UserSession Student(int id = 1) => new(AccountRole.Student, id, "Mert Deneme");

    public static UserSession Teacher(int id = 1) => new(AccountRole.Teacher, id, "Deniz Örnek");
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
}

/// <summary>Collects the audit events instead of writing them anywhere.</summary>
internal sealed class RecordingAudit : IAuditLog
{
    public List<(AuditEvent Event, AccountRole? Role)> Events { get; } = [];

    public void Record(AuditEvent auditEvent, AccountRole? role) => Events.Add((auditEvent, role));
}
