using System.Diagnostics;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.UI.Infrastructure;

/// <summary>
/// Writes audit events to the trace listeners as "audit {code} role={role}": the fixed code and role
/// only, never a name, e-mail address, password or id.
/// </summary>
internal sealed class TraceAuditLog : IAuditLog
{
    public void Record(AuditEvent auditEvent, AccountRole? role) =>
        Trace.TraceInformation(Format(auditEvent, role));

    internal static string Format(AuditEvent auditEvent, AccountRole? role) =>
        $"audit {AuditCodes.Of(auditEvent)} role={AuditCodes.Of(role)}";
}
