using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Domain.Repositories;

/// <summary>Which students joined which courses.</summary>
public interface IEnrollmentRepository
{
    /// <summary>Adds the student to the course.</summary>
    /// <exception cref="AlreadyEnrolledException">The student already joined the course.</exception>
    Task AddAsync(int studentId, int courseId, CancellationToken cancellationToken = default);

    Task<bool> IsEnrolledAsync(int studentId, int courseId, CancellationToken cancellationToken = default);

    /// <summary>The courses the student joined, by course id.</summary>
    Task<IReadOnlyList<CourseSummary>> GetCoursesOfStudentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Names of the students who joined the course, by name.</summary>
    Task<IReadOnlyList<string>> GetStudentNamesAsync(int courseId, CancellationToken cancellationToken = default);

    Task<int> CountStudentsAsync(int courseId, CancellationToken cancellationToken = default);
}

/// <summary>Raised when a student already joined a course.</summary>
public sealed class AlreadyEnrolledException : Exception
{
    public AlreadyEnrolledException()
        : base("The student already joined the course.")
    {
    }

    public AlreadyEnrolledException(string message)
        : base(message)
    {
    }

    public AlreadyEnrolledException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
