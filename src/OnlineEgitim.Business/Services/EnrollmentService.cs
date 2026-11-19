using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Services;

/// <summary>Joining courses, a student's own courses and a teacher's view of who joined.</summary>
public interface IEnrollmentService
{
    /// <summary>Adds the signed-in student to the course.</summary>
    Task EnrollAsync(UserSession session, int courseId, CancellationToken cancellationToken = default);

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
    IAuditLog audit) : IEnrollmentService
{
    internal const string CourseRequired = "Lütfen bir ders seçiniz.";
    internal const string AlreadyEnrolled = "Bu dersi zaten seçtiniz.";
    internal const string CourseFull = "Bu dersin kontenjanı dolu.";

    public async Task EnrollAsync(UserSession session, int courseId, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Student, audit);

        if (courseId <= 0)
        {
            throw new BusinessRuleException(CourseRequired);
        }

        // The repository decides everything in one transaction under the course's lock: the course
        // exists, the student is not in it yet and a seat is free. The primary key (student, course)
        // stays the last guard against a double enrollment.
        var status = await enrollments.AddAsync(session.UserId, courseId, cancellationToken).ConfigureAwait(false);
        switch (status)
        {
            case EnrollmentStatus.Enrolled:
                audit.Record(AuditEvent.Enrolled, AccountRole.Student);
                return;
            case EnrollmentStatus.AlreadyEnrolled:
                throw new BusinessRuleException(AlreadyEnrolled);
            case EnrollmentStatus.CourseFull:
                throw new BusinessRuleException(CourseFull);
            default:
                throw Access.CourseNotFoundFor(session, audit);
        }
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
}
