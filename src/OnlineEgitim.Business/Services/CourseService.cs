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

/// <summary>
/// A catalogue search of a student. The texts are matched anywhere in the course name, topic and teacher
/// name, literally (no wildcards); empty texts do not filter. Pages are counted from 0.
/// </summary>
public sealed record CourseSearch(
    string? Name = null,
    string? Topic = null,
    string? TeacherName = null,
    bool OnlyWithFreeSeats = false,
    EnrollmentFilter Enrollment = EnrollmentFilter.Any,
    int PageIndex = 0,
    int PageSize = CourseSearch.DefaultPageSize)
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 50;
}

/// <summary>One page of a catalogue search; <see cref="PageIndex"/> is the page actually returned.</summary>
public sealed record CatalogResult(IReadOnlyList<CatalogEntry> Items, int TotalCount, int PageIndex, int PageSize)
{
    /// <summary>Number of pages, at least one.</summary>
    public int PageCount => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
}

/// <summary>Course catalogue, course details, course creation and notes.</summary>
public interface ICourseService
{
    /// <summary>Every course, for students choosing one to join.</summary>
    Task<IReadOnlyList<CourseSummary>> GetCatalogAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of the catalogue for the signed-in student, by course name and then id, with the number
    /// of all matching courses. A page past the end returns the last page.
    /// </summary>
    Task<CatalogResult> SearchCatalogAsync(UserSession session, CourseSearch search, CancellationToken cancellationToken = default);

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
    ISessionGuard sessions,
    IValidator<CourseInput> courseValidator,
    IValidator<NoteInput> noteValidator,
    IValidator<CourseSearch> searchValidator) : ICourseService
{
    internal const string StudentsChanged =
        "Siz onaylarken bu kursa öğrenci katıldı veya ayrıldı. Kurs silinmedi; lütfen yeniden deneyin.";

    internal static string CapacityBelowStudents(int students) => string.Create(
        CultureInfo.InvariantCulture,
        $"Kontenjan, kayıtlı öğrenci sayısının ({students}) altına düşürülemez.");

    public async Task<IReadOnlyList<CourseSummary>> GetCatalogAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Student, audit);
        await sessions.EnsureCurrentAsync(session, cancellationToken).ConfigureAwait(false);
        return await courses.GetAllAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CatalogResult> SearchCatalogAsync(UserSession session, CourseSearch search, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Student, audit);
        await sessions.EnsureCurrentAsync(session, cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(search);
        searchValidator.ValidateOrThrow(search);

        var pageIndex = search.PageIndex;
        var page = await courses.SearchAsync(Criteria(session, search, pageIndex), cancellationToken).ConfigureAwait(false);
        if (page.Items.Count == 0 && pageIndex > 0)
        {
            // The list shrank since the page was chosen (or a page past the end was asked for): the
            // last page is shown, or the first one when nothing matches.
            pageIndex = page.TotalCount == 0 ? 0 : (page.TotalCount - 1) / search.PageSize;
            if (page.TotalCount > 0)
            {
                page = await courses.SearchAsync(Criteria(session, search, pageIndex), cancellationToken).ConfigureAwait(false);
            }
        }

        return new CatalogResult(page.Items, page.TotalCount, pageIndex, search.PageSize);
    }

    private static CourseSearchCriteria Criteria(UserSession session, CourseSearch search, int pageIndex) =>
        new(
            session.UserId,
            InputRules.Clean(search.Name),
            InputRules.Clean(search.Topic),
            InputRules.Clean(search.TeacherName),
            search.OnlyWithFreeSeats,
            search.Enrollment,
            pageIndex * search.PageSize,
            search.PageSize);

    public async Task<IReadOnlyList<CourseSummary>> GetTeachingAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Teacher, audit);
        await sessions.EnsureCurrentAsync(session, cancellationToken).ConfigureAwait(false);
        return await courses.GetByTeacherAsync(session.UserId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TeacherProfile> GetMyProfileAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Teacher, audit);
        await sessions.EnsureCurrentAsync(session, cancellationToken).ConfigureAwait(false);
        return await accounts.GetTeacherAsync(session.UserId, cancellationToken).ConfigureAwait(false)
            ?? throw new AccessDeniedException();
    }

    public async Task<CourseDetails> GetDetailsAsync(UserSession session, int courseId, CancellationToken cancellationToken = default)
    {
        Access.RequireStudentOrTeacher(session, audit);
        await sessions.EnsureCurrentAsync(session, cancellationToken).ConfigureAwait(false);

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
        await sessions.EnsureCurrentAsync(session, cancellationToken).ConfigureAwait(false);

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
        await sessions.EnsureCurrentAsync(session, cancellationToken).ConfigureAwait(false);

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
        await sessions.EnsureCurrentAsync(session, cancellationToken).ConfigureAwait(false);

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
        await sessions.EnsureCurrentAsync(session, cancellationToken).ConfigureAwait(false);

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
