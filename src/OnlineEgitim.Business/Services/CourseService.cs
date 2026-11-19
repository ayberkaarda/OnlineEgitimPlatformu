using System.Globalization;
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
    int StudentCount);

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

    /// <summary>Creates a course owned by the signed-in teacher; returns its id.</summary>
    Task<int> CreateAsync(UserSession session, string? name, string? topic, CancellationToken cancellationToken = default);

    /// <summary>Adds a note to one of the signed-in teacher's courses.</summary>
    Task AddNoteAsync(UserSession session, int courseId, string? content, CancellationToken cancellationToken = default);
}

internal sealed class CourseService(
    ICourseRepository courses,
    IEnrollmentRepository enrollments,
    IAccountRepository accounts,
    IAuditLog audit) : ICourseService
{
    internal const string FieldsRequired = "Lütfen tüm alanları doldurun.";
    internal const string NoteRequired = "Lütfen bir kurs seçin ve not içeriğini girin.";

    internal static readonly string NameTooLong = string.Create(
        CultureInfo.InvariantCulture,
        $"Kurs adı en fazla {InputRules.CourseNameMaxLength} karakter olabilir.");

    internal static readonly string TopicTooLong = string.Create(
        CultureInfo.InvariantCulture,
        $"Konu en fazla {InputRules.TopicMaxLength} karakter olabilir.");

    internal static readonly string NoteTooLong = string.Create(
        CultureInfo.InvariantCulture,
        $"Not en fazla {InputRules.NoteMaxLength} karakter olabilir.");

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
            return new CourseDetails(course.Id, course.Name, course.Topic, course.TeacherName, true, notes, students, count);
        }

        // The catalogue is public, the notes are for the course's students and the names of the other
        // students are not shown to students at all.
        var enrolled = await enrollments.IsEnrolledAsync(session.UserId, course.Id, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> visibleNotes = enrolled
            ? await courses.GetNotesAsync(course.Id, cancellationToken).ConfigureAwait(false)
            : [];
        return new CourseDetails(course.Id, course.Name, course.Topic, course.TeacherName, enrolled, visibleNotes, null, count);
    }

    public async Task<int> CreateAsync(UserSession session, string? name, string? topic, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Teacher, audit);

        var courseName = InputRules.Clean(name);
        var courseTopic = InputRules.Clean(topic);
        if (courseName is null || courseTopic is null)
        {
            throw new BusinessRuleException(FieldsRequired);
        }

        if (!InputRules.IsValidText(courseName, InputRules.CourseNameMaxLength))
        {
            throw new BusinessRuleException(NameTooLong);
        }

        if (!InputRules.IsValidText(courseTopic, InputRules.TopicMaxLength))
        {
            throw new BusinessRuleException(TopicTooLong);
        }

        var id = await courses.AddAsync(
            new Course { Name = courseName, Topic = courseTopic, TeacherId = session.UserId },
            cancellationToken).ConfigureAwait(false);
        audit.Record(AuditEvent.CourseCreated, AccountRole.Teacher);
        return id;
    }

    public async Task AddNoteAsync(UserSession session, int courseId, string? content, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(session, AccountRole.Teacher, audit);

        var text = InputRules.Clean(content);
        if (courseId <= 0 || text is null)
        {
            throw new BusinessRuleException(NoteRequired);
        }

        if (!InputRules.IsValidMultilineText(text, InputRules.NoteMaxLength))
        {
            throw new BusinessRuleException(NoteTooLong);
        }

        // Ownership is checked inside the repository's transaction; a missing and a foreign course
        // get the same answer.
        if (!await courses.AddNoteAsync(courseId, session.UserId, text, cancellationToken).ConfigureAwait(false))
        {
            throw Access.CourseNotFoundFor(session, audit);
        }

        audit.Record(AuditEvent.NoteAdded, AccountRole.Teacher);
    }
}
