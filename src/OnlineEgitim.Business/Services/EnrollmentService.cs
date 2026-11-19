using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Services;

/// <summary>Joining and leaving courses, a student's own courses and a teacher's view of who joined.</summary>
public interface IEnrollmentService
{
    /// <summary>
    /// Adds the signed-in student to the course while it has a free seat. A student who left the course
    /// can join it again from the next calendar day on (local time), again only while a seat is free.
    /// </summary>
    Task EnrollAsync(UserSession session, int courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes the signed-in student out of the course at once: the seat is free for others and the
    /// course's notes are closed to the student until they join again.
    /// </summary>
    Task LeaveAsync(UserSession session, int courseId, CancellationToken cancellationToken = default);

    /// <summary>The courses the signed-in student joined; nobody else's.</summary>
    Task<IReadOnlyList<CourseSummary>> GetMyCoursesAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Names of the students of one of the signed-in teacher's courses; any other id is answered like
    /// a missing course.
    /// </summary>
    Task<IReadOnlyList<string>> GetStudentsAsync(UserSession session, int courseId, CancellationToken cancellationToken = default);
}

internal sealed class EnrollmentService(
    IEnrollmentRepository enrollments,
    ICourseRepository courses,
    TimeProvider timeProvider,
    IAuditLog audit) : IEnrollmentService
{
    internal const string CourseRequired = "Lütfen bir ders seçiniz.";
    internal const string AlreadyEnrolled = "Bu dersi zaten seçtiniz.";
    internal const string CourseFull = "Bu dersin kontenjanı dolu.";
    internal const string NotEnrolled = "Bu derse kayıtlı değilsiniz.";
    internal const string RejoinTooSoon = "Ayrıldığınız bir derse aynı gün yeniden kaydolamazsınız. Yarın tekrar deneyebilirsiniz.";

    public async Task EnrollAsync(UserSession session, int courseId, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Student, audit);

        if (courseId <= 0)
        {
            throw new BusinessRuleException(CourseRequired);
        }

        // The repository decides everything in one transaction under the course's lock: the course
        // exists, the student is not in it yet, did not leave it today and a seat is free. The
        // primary key (student, course) stays the last guard against a double enrollment.
        var status = await enrollments.AddAsync(session.UserId, courseId, StartOfToday(), cancellationToken).ConfigureAwait(false);
        switch (status)
        {
            case EnrollmentStatus.Enrolled:
                audit.Record(AuditEvent.Enrolled, AccountRole.Student);
                return;
            case EnrollmentStatus.AlreadyEnrolled:
                throw new BusinessRuleException(AlreadyEnrolled);
            case EnrollmentStatus.CourseFull:
                throw new BusinessRuleException(CourseFull);
            case EnrollmentStatus.RejoinTooSoon:
                throw new BusinessRuleException(RejoinTooSoon);
            default:
                throw Access.CourseNotFoundFor(session, audit);
        }
    }

    public async Task LeaveAsync(UserSession session, int courseId, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Student, audit);

        if (courseId <= 0)
        {
            throw new BusinessRuleException(CourseRequired);
        }

        // Only the session's own enrollment can be closed; the student id never comes from the caller.
        if (!await enrollments.LeaveAsync(session.UserId, courseId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleException(NotEnrolled);
        }

        audit.Record(AuditEvent.EnrollmentLeft, AccountRole.Student);
    }

    public Task<IReadOnlyList<CourseSummary>> GetMyCoursesAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Student, audit);
        return enrollments.GetCoursesOfStudentAsync(session.UserId, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetStudentsAsync(UserSession session, int courseId, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Teacher, audit);

        var course = courseId > 0
            ? await courses.GetByIdAsync(courseId, cancellationToken).ConfigureAwait(false)
            : null;
        if (course is null || course.TeacherId != session.UserId)
        {
            throw Access.CourseNotFoundFor(session, audit);
        }

        return await enrollments.GetStudentNamesAsync(courseId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// UTC time of the last local midnight: a student who left at or after it left "today" and may join
    /// the course again only from the next local day.
    /// </summary>
    private DateTime StartOfToday()
    {
        var zone = timeProvider.LocalTimeZone;
        var localToday = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone).Date;
        return zone.IsInvalidTime(localToday)
            ? timeProvider.GetUtcNow().UtcDateTime.Date
            : TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localToday, DateTimeKind.Unspecified), zone);
    }
}
