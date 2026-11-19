using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Domain.Repositories;

/// <summary>
/// Which students joined which courses. Every query counts and returns active enrollments of courses
/// that are not deleted only.
/// </summary>
public interface IEnrollmentRepository
{
    /// <summary>
    /// Adds the student to the course in one transaction under the course's lock: the course must exist
    /// and not be deleted, and the active enrollments are counted against its capacity while no other
    /// enrollment, capacity change or deletion of the course can run.
    /// </summary>
    Task<EnrollmentStatus> AddAsync(int studentId, int courseId, CancellationToken cancellationToken = default);

    Task<bool> IsEnrolledAsync(int studentId, int courseId, CancellationToken cancellationToken = default);

    /// <summary>The courses the student joined, by course id.</summary>
    Task<IReadOnlyList<CourseSummary>> GetCoursesOfStudentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Names of the students who joined the course, by name.</summary>
    Task<IReadOnlyList<string>> GetStudentNamesAsync(int courseId, CancellationToken cancellationToken = default);

    Task<int> CountStudentsAsync(int courseId, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of <see cref="IEnrollmentRepository.AddAsync"/>.</summary>
public enum EnrollmentStatus
{
    /// <summary>The student is enrolled now.</summary>
    Enrolled = 0,

    /// <summary>The student had an active enrollment already; nothing was written.</summary>
    AlreadyEnrolled = 1,

    /// <summary>The course does not exist or was deleted; nothing was written.</summary>
    CourseNotFound = 2,

    /// <summary>Every seat is taken (or the capacity is zero); nothing was written.</summary>
    CourseFull = 3,
}
