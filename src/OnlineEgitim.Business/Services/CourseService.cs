using System.Globalization;
using FluentValidation;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Services;

/// <summary>
/// A course as the detail screen shows it. <see cref="Notes"/> is filled for the course's teacher and
/// for students who joined it; <see cref="Students"/> (names) only for the course's teacher, other
/// viewers get the count alone.
/// </summary>
public sealed record CourseDetails(
    int Id,
    string Name,
    string? Topic,
    string TeacherName,
    bool NotesVisible,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string>? Students,
    int StudentCount,
    int? Capacity = null);

/// <summary>Course catalogue, course details, course creation and notes.</summary>
public interface ICourseService
{
    /// <summary>Every course, for students choosing one to join.</summary>
    Task<IReadOnlyList<CourseSummary>> GetCatalogAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>The signed-in teacher's own courses.</summary>
    Task<IReadOnlyList<CourseSummary>> GetTeachingAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>The signed-in teacher's name and specialty, for the course form.</summary>
    Task<TeacherProfile> GetMyProfileAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Details of a course. A teacher gets only their own courses; any other id is answered like a
    /// missing course.
    /// </summary>
    Task<CourseDetails> GetDetailsAsync(UserSession session, int courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a course owned by the signed-in teacher; returns its id. <paramref name="capacity"/> is
    /// the number of seats (0 to 1000), or <see langword="null"/> for no limit.
    /// </summary>
    Task<int> CreateAsync(UserSession session, string? name, string? topic, int? capacity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes name, topic and capacity of one of the signed-in teacher's courses. The capacity cannot
    /// go below the number of students in the course. Any other id is answered like a missing course.
    /// </summary>
    Task UpdateAsync(UserSession session, int courseId, string? name, string? topic, int? capacity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one of the signed-in teacher's courses. <paramref name="confirmedStudents"/> is the number
    /// of enrolled students the teacher confirmed the deletion for (0 when the course is empty); when
    /// the course has a different number by then, nothing is deleted and the teacher is asked again.
    /// The course is kept as deleted with its notes, and its enrollments are closed, so it disappears
    /// from every list and its notes from every student. Any other id is answered like a missing course.
    /// </summary>
    Task DeleteAsync(UserSession session, int courseId, int confirmedStudents, CancellationToken cancellationToken = default);

    /// <summary>Adds a note to one of the signed-in teacher's courses.</summary>
    Task AddNoteAsync(UserSession session, int courseId, string? content, CancellationToken cancellationToken = default);
}

internal sealed class CourseService(
    ICourseRepository courses,
    IEnrollmentRepository enrollments,
    IAccountRepository accounts,
    TimeProvider timeProvider,
    IAuditLog audit,
    IValidator<CourseInput> courseValidator,
    IValidator<NoteInput> noteValidator) : ICourseService
{
    internal const string StudentsChanged =
        "Siz onaylarken bu kursa öğrenci katıldı veya ayrıldı. Kurs silinmedi; lütfen yeniden deneyin.";

    internal static string CapacityBelowStudents(int students) => string.Create(
        CultureInfo.InvariantCulture,
        $"Kontenjan, kayıtlı öğrenci sayısının ({students}) altına düşürülemez.");

    public Task<IReadOnlyList<CourseSummary>> GetCatalogAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Student, audit);
        return courses.GetAllAsync(cancellationToken);
    }

    public Task<IReadOnlyList<CourseSummary>> GetTeachingAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Teacher, audit);
        return courses.GetByTeacherAsync(session.UserId, cancellationToken);
    }

    public async Task<TeacherProfile> GetMyProfileAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Teacher, audit);
        return await accounts.GetTeacherAsync(session.UserId, cancellationToken).ConfigureAwait(false)
            ?? throw new AccessDeniedException();
    }

    public async Task<CourseDetails> GetDetailsAsync(UserSession session, int courseId, CancellationToken cancellationToken = default)
    {
        Access.RequireSignedIn(session);

        var course = courseId > 0
            ? await courses.GetByIdAsync(courseId, cancellationToken).ConfigureAwait(false)
            : null;
        if (course is null || (session.IsTeacher && course.TeacherId != session.UserId))
        {
            throw Access.CourseNotFoundFor(session, audit);
        }

        var count = await enrollments.CountStudentsAsync(course.Id, cancellationToken).ConfigureAwait(false);
        if (session.IsTeacher)
        {
            var notes = await courses.GetNotesAsync(course.Id, cancellationToken).ConfigureAwait(false);
            var students = await enrollments.GetStudentNamesAsync(course.Id, cancellationToken).ConfigureAwait(false);
            return new CourseDetails(course.Id, course.Name, course.Topic, course.TeacherName, true, notes, students, count, course.Capacity);
        }

        // The catalogue is public, the notes are for the course's students and the names of the other
        // students are not shown to students at all.
        var enrolled = await enrollments.IsEnrolledAsync(session.UserId, course.Id, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> visibleNotes = enrolled
            ? await courses.GetNotesAsync(course.Id, cancellationToken).ConfigureAwait(false)
            : [];
        return new CourseDetails(course.Id, course.Name, course.Topic, course.TeacherName, enrolled, visibleNotes, null, count, course.Capacity);
    }

    public async Task<int> CreateAsync(UserSession session, string? name, string? topic, int? capacity, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Teacher, audit);

        var input = CourseInput.Create(name, topic, capacity);
        courseValidator.ValidateOrThrow(input);

        var id = await courses.AddAsync(
            new Course { Name = input.Name!, Topic = input.Topic, TeacherId = session.UserId, Capacity = input.Capacity },
            cancellationToken).ConfigureAwait(false);
        audit.Record(AuditEvent.CourseCreated, AccountRole.Teacher);
        return id;
    }

    public async Task UpdateAsync(UserSession session, int courseId, string? name, string? topic, int? capacity, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Teacher, audit);

        var input = CourseInput.Create(name, topic, capacity);
        courseValidator.ValidateOrThrow(input);

        // Ownership and the capacity rule are decided inside the repository's transaction, under the
        // course's lock; a missing, deleted and foreign course get the same answer.
        var result = courseId > 0
            ? await courses.UpdateAsync(courseId, session.UserId, input.Name!, input.Topic, input.Capacity, cancellationToken).ConfigureAwait(false)
            : new CourseWriteResult(CourseWriteStatus.NotFound, 0);
        switch (result.Status)
        {
            case CourseWriteStatus.Done:
                audit.Record(AuditEvent.CourseUpdated, AccountRole.Teacher);
                return;
            case CourseWriteStatus.CapacityBelowEnrollments:
                throw new BusinessRuleException(CapacityBelowStudents(result.StudentCount));
            default:
                throw Access.CourseNotFoundFor(session, audit);
        }
    }

    public async Task DeleteAsync(UserSession session, int courseId, int confirmedStudents, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Teacher, audit);

        var result = courseId > 0 && confirmedStudents >= 0
            ? await courses.DeleteAsync(courseId, session.UserId, confirmedStudents, timeProvider.GetUtcNow().UtcDateTime, cancellationToken).ConfigureAwait(false)
            : new CourseWriteResult(CourseWriteStatus.NotFound, 0);
        switch (result.Status)
        {
            case CourseWriteStatus.Done:
                audit.Record(AuditEvent.CourseDeleted, AccountRole.Teacher);
                return;
            case CourseWriteStatus.StudentsChanged:
                throw new BusinessRuleException(StudentsChanged);
            default:
                throw Access.CourseNotFoundFor(session, audit);
        }
    }

    public async Task AddNoteAsync(UserSession session, int courseId, string? content, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Teacher, audit);

        var input = NoteInput.Create(courseId, content);
        noteValidator.ValidateOrThrow(input);
        var text = input.Content!;

        // Ownership is checked inside the repository's transaction; a missing and a foreign course
        // get the same answer.
        if (!await courses.AddNoteAsync(courseId, session.UserId, text, cancellationToken).ConfigureAwait(false))
        {
            throw Access.CourseNotFoundFor(session, audit);
        }

        audit.Record(AuditEvent.NoteAdded, AccountRole.Teacher);
    }
}
