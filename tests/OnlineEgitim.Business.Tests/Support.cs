using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Business.Tests;

internal static class Sessions
{
    public static UserSession Student(int id = 1) => new(AccountRole.Student, id, "Mert Deneme");

    public static UserSession Teacher(int id = 1) => new(AccountRole.Teacher, id, "Deniz Örnek");
}

/// <summary>Collects the audit events instead of writing them anywhere.</summary>
internal sealed class RecordingAudit : IAuditLog
{
    public List<(AuditEvent Event, AccountRole? Role)> Events { get; } = [];

    public void Record(AuditEvent auditEvent, AccountRole? role) => Events.Add((auditEvent, role));
}
