using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Domain.Repositories;

/// <summary>Courses and their notes. Deleted courses are never returned.</summary>
public interface ICourseRepository
{
    /// <summary>Every course with its teacher, by id.</summary>
    Task<IReadOnlyList<CourseSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The courses of one teacher, by id.</summary>
    Task<IReadOnlyList<CourseSummary>> GetByTeacherAsync(int teacherId, CancellationToken cancellationToken = default);

    Task<CourseSummary?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Stores a new course and returns its id.</summary>
    Task<int> AddAsync(Course course, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes name, topic and capacity of a course of <paramref name="teacherId"/>. Runs under the
    /// course's lock, the one enrollments take, so the capacity is compared with the number of active
    /// enrollments that no concurrent enrollment can change before the commit.
    /// </summary>
    Task<CourseWriteResult> UpdateAsync(int courseId, int teacherId, string name, string? topic, int? capacity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a course of <paramref name="teacherId"/> (it is marked deleted, see
    /// <see cref="Course.DeletedUtc"/>) and closes its active enrollments at <paramref name="now"/>, in one
    /// transaction under the course's lock. Only when the course still has exactly
    /// <paramref name="expectedStudents"/> active enrollments, the number the teacher confirmed.
    /// </summary>
    Task<CourseWriteResult> DeleteAsync(int courseId, int teacherId, int expectedStudents, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>The notes of a course, oldest first.</summary>
    Task<IReadOnlyList<string>> GetNotesAsync(int courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a note to the course only if it belongs to <paramref name="teacherId"/>; the ownership is
    /// checked in the same transaction as the insert.
    /// </summary>
    /// <returns><see langword="false"/> when no course of that teacher matched; nothing is written then.</returns>
    Task<bool> AddNoteAsync(int courseId, int teacherId, string content, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a change to an existing course.</summary>
public enum CourseWriteStatus
{
    /// <summary>The change was stored.</summary>
    Done = 0,

    /// <summary>No course of that teacher has the id (missing, deleted or another teacher's); nothing was written.</summary>
    NotFound = 1,

    /// <summary>The new capacity is below the number of active enrollments, given in <see cref="CourseWriteResult.StudentCount"/>.</summary>
    CapacityBelowEnrollments = 2,

    /// <summary>The number of active enrollments is not the confirmed one any more; it is in <see cref="CourseWriteResult.StudentCount"/>.</summary>
    StudentsChanged = 3,
}

/// <summary>Result of <see cref="ICourseRepository.UpdateAsync"/> and <see cref="ICourseRepository.DeleteAsync"/>.</summary>
public readonly record struct CourseWriteResult(CourseWriteStatus Status, int StudentCount);
