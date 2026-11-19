using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Business.Services;

/// <summary>Role checks shared by the services.</summary>
internal static class Access
{
    /// <summary>The single answer for a course that does not exist or is not the caller's.</summary>
    internal const string CourseNotFound = "Ders bulunamadı.";

    public static void RequireRole(UserSession? session, AccountRole role, IAuditLog audit)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Role != role)
        {
            audit.Record(AuditEvent.AccessDenied, session.Role);
            throw new AccessDeniedException();
        }
    }

    public static void RequireSignedIn(UserSession? session) => ArgumentNullException.ThrowIfNull(session);

    /// <summary>
    /// Course views are for students and teachers. An administrator's id belongs to the staff table and
    /// must never be compared with student ids, so administrators are refused outright.
    /// </summary>
    public static void RequireStudentOrTeacher(UserSession? session, IAuditLog audit)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Role is not (AccountRole.Student or AccountRole.Teacher))
        {
            audit.Record(AuditEvent.AccessDenied, session.Role);
            throw new AccessDeniedException();
        }
    }

    /// <summary>
    /// Refuses a course that is missing or belongs to someone else with the same exception, message
    /// and audit event, so ids cannot be probed.
    /// </summary>
    public static BusinessRuleException CourseNotFoundFor(UserSession session, IAuditLog audit)
    {
        audit.Record(AuditEvent.AccessDenied, session.Role);
        return new BusinessRuleException(CourseNotFound);
    }
}
