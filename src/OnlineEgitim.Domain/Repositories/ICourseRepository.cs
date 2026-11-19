using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Domain.Repositories;

/// <summary>Courses and their notes.</summary>
public interface ICourseRepository
{
    /// <summary>Every course with its teacher, by id.</summary>
    Task<IReadOnlyList<CourseSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The courses of one teacher, by id.</summary>
    Task<IReadOnlyList<CourseSummary>> GetByTeacherAsync(int teacherId, CancellationToken cancellationToken = default);

    Task<CourseSummary?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Stores a new course and returns its id.</summary>
    Task<int> AddAsync(Course course, CancellationToken cancellationToken = default);

    /// <summary>The notes of a course, oldest first.</summary>
    Task<IReadOnlyList<string>> GetNotesAsync(int courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a note to the course only if it belongs to <paramref name="teacherId"/>; the ownership is
    /// checked in the same transaction as the insert.
    /// </summary>
    /// <returns><see langword="false"/> when no course of that teacher matched; nothing is written then.</returns>
    Task<bool> AddNoteAsync(int courseId, int teacherId, string content, CancellationToken cancellationToken = default);
}
