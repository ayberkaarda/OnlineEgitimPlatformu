using Microsoft.Extensions.Time.Testing;
using Moq;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Tests;

/// <summary>Role and ownership rules of courses and notes, with the repositories mocked.</summary>
public sealed class CourseServiceTests
{
    private static readonly CourseSummary DenizCourse = new(3, "Temel Programlama", 1, "Deniz Örnek", "Döngüler");

    private readonly Mock<ICourseRepository> _courses = new(MockBehavior.Strict);
    private readonly Mock<IEnrollmentRepository> _enrollments = new(MockBehavior.Strict);
    private readonly Mock<IAccountRepository> _accounts = new(MockBehavior.Strict);
    private readonly RecordingAudit _audit = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.Zero));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private CourseService Service => new(_courses.Object, _enrollments.Object, _accounts.Object, _clock, _audit, CurrentSessions.Instance, Validators.Course, Validators.Note, Validators.CourseSearch);

    [Fact]
    public async Task Details_ForeignAndMissingCourse_SameAnswerSameAuditAndNoDataRead()
    {
        _courses.Setup(c => c.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(DenizCourse);
        _courses.Setup(c => c.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((CourseSummary?)null);

        var foreign = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.GetDetailsAsync(Sessions.Teacher(2), 3, Ct));
        var missing = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.GetDetailsAsync(Sessions.Teacher(2), 99, Ct));

        Assert.Equal(missing.Message, foreign.Message);
        Assert.Equal(Access.CourseNotFound, foreign.Message);
        Assert.Equal([(AuditEvent.AccessDenied, (AccountRole?)AccountRole.Teacher), (AuditEvent.AccessDenied, (AccountRole?)AccountRole.Teacher)], _audit.Events);
        _enrollments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Details_OwnCourse_ShowsNotesAndStudentNames()
    {
        _courses.Setup(c => c.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(DenizCourse);
        _courses.Setup(c => c.GetNotesAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(["Not"]);
        _enrollments.Setup(e => e.CountStudentsAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _enrollments.Setup(e => e.GetStudentNamesAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(["Mert Deneme"]);

        var details = await Service.GetDetailsAsync(Sessions.Teacher(1), 3, Ct);

        Assert.Equal(["Not"], details.Notes);
        Assert.Equal(["Mert Deneme"], details.Students);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Details_Student_NotesOnlyWhenEnrolledAndNeverOtherStudentsNames(bool enrolled)
    {
        _courses.Setup(c => c.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(DenizCourse);
        _courses.Setup(c => c.GetNotesAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(["Not"]);
        _enrollments.Setup(e => e.CountStudentsAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(4);
        _enrollments.Setup(e => e.IsEnrolledAsync(5, 3, It.IsAny<CancellationToken>())).ReturnsAsync(enrolled);

        var details = await Service.GetDetailsAsync(Sessions.Student(5), 3, Ct);

        Assert.Equal(enrolled, details.NotesVisible);
        Assert.Equal(enrolled ? ["Not"] : [], details.Notes);
        Assert.Null(details.Students);
        Assert.Equal(4, details.StudentCount);
        _enrollments.Verify(e => e.GetStudentNamesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _courses.Verify(c => c.GetNotesAsync(3, It.IsAny<CancellationToken>()), enrolled ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task AddNote_ForeignCourse_IsRefusedLikeAMissingOne()
    {
        _courses.Setup(c => c.AddNoteAsync(3, 2, "Not", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.AddNoteAsync(Sessions.Teacher(2), 3, " Not ", Ct));

        Assert.Equal(Access.CourseNotFound, error.Message);
        Assert.Equal([(AuditEvent.AccessDenied, (AccountRole?)AccountRole.Teacher)], _audit.Events);
    }

    [Theory]
    [InlineData(0, "Not", "Lütfen bir kurs seçin ve not içeriğini girin.")]
    [InlineData(3, "   ", "Lütfen bir kurs seçin ve not içeriğini girin.")]
    [InlineData(3, null, "Lütfen bir kurs seçin ve not içeriğini girin.")]
    public async Task AddNote_MissingInput_IsRefusedBeforeAnyWrite(int courseId, string? content, string message)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.AddNoteAsync(Sessions.Teacher(), courseId, content, Ct));

        Assert.Equal(message, error.Message);
        _courses.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddNote_TooLongOrWithControlCharacters_IsRefused()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service.AddNoteAsync(Sessions.Teacher(), 3, new string('x', 501), Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service.AddNoteAsync(Sessions.Teacher(), 3, "a\0b", Ct));
        _courses.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddNote_MultiLineText_IsAccepted()
    {
        _courses.Setup(c => c.AddNoteAsync(3, 1, "Satır 1\r\nSatır 2", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Service.AddNoteAsync(Sessions.Teacher(), 3, "Satır 1\r\nSatır 2", Ct);

        Assert.Equal([(AuditEvent.NoteAdded, (AccountRole?)AccountRole.Teacher)], _audit.Events);
    }

    [Fact]
    public async Task Create_OwnedByTheSessionNotByAnyInput()
    {
        Course? stored = null;
        _courses.Setup(c => c.AddAsync(It.IsAny<Course>(), It.IsAny<CancellationToken>()))
            .Callback<Course, CancellationToken>((c, _) => stored = c)
            .ReturnsAsync(9);

        var id = await Service.CreateAsync(Sessions.Teacher(4), " Grafik ", " Renk ", null, Ct);

        Assert.Equal(9, id);
        Assert.NotNull(stored);
        Assert.Equal(4, stored.TeacherId);
        Assert.Equal("Grafik", stored.Name);
        Assert.Equal("Renk", stored.Topic);
    }

    [Theory]
    [InlineData(null, "Konu")]
    [InlineData("Ders", "  ")]
    public async Task Create_MissingField_IsRefused(string? name, string? topic)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CreateAsync(Sessions.Teacher(), name, topic, null, Ct));

        Assert.Equal(ValidationMessages.CourseFieldsRequired, error.Message);
        _courses.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_TooLongValues_AreRefused()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CreateAsync(Sessions.Teacher(), new string('x', 101), "Konu", null, Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CreateAsync(Sessions.Teacher(), "Ders", new string('x', 201), null, Ct));
        _courses.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1000)]
    public async Task Create_CapacityWithinLimits_IsStored(int? capacity)
    {
        Course? stored = null;
        _courses.Setup(c => c.AddAsync(It.IsAny<Course>(), It.IsAny<CancellationToken>()))
            .Callback<Course, CancellationToken>((c, _) => stored = c)
            .ReturnsAsync(9);

        await Service.CreateAsync(Sessions.Teacher(4), "Grafik", "Renk", capacity, Ct);

        Assert.Equal(capacity, stored!.Capacity);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1001)]
    [InlineData(int.MinValue)]
    public async Task CreateOrUpdate_CapacityOutOfRange_IsRefusedBeforeAnyWrite(int capacity)
    {
        var create = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CreateAsync(Sessions.Teacher(), "Ders", "Konu", capacity, Ct));
        var update = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.UpdateAsync(Sessions.Teacher(), 3, "Ders", "Konu", capacity, Ct));

        Assert.Equal(ValidationMessages.CapacityInvalid, create.Message);
        Assert.Equal(ValidationMessages.CapacityInvalid, update.Message);
        _courses.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Update_ForeignOrMissingCourse_SameAnswerAsMissingAndAudited()
    {
        _courses.Setup(c => c.UpdateAsync(3, 2, "Ders", "Konu", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CourseWriteResult(CourseWriteStatus.NotFound, 0));

        var foreign = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.UpdateAsync(Sessions.Teacher(2), 3, " Ders ", " Konu ", 5, Ct));
        var none = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.UpdateAsync(Sessions.Teacher(2), 0, "Ders", "Konu", 5, Ct));

        Assert.Equal(Access.CourseNotFound, foreign.Message);
        Assert.Equal(Access.CourseNotFound, none.Message);
        Assert.Equal(2, _audit.Events.Count(e => e.Event == AuditEvent.AccessDenied));
    }

    [Fact]
    public async Task Update_CapacityBelowStudents_NamesTheCountAndChangesNothing()
    {
        _courses.Setup(c => c.UpdateAsync(3, 1, "Ders", "Konu", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CourseWriteResult(CourseWriteStatus.CapacityBelowEnrollments, 3));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.UpdateAsync(Sessions.Teacher(1), 3, "Ders", "Konu", 2, Ct));

        Assert.Equal("Kontenjan, kayıtlı öğrenci sayısının (3) altına düşürülemez.", error.Message);
        Assert.Empty(_audit.Events);
    }

    [Fact]
    public async Task Update_Accepted_IsAudited()
    {
        _courses.Setup(c => c.UpdateAsync(3, 1, "Ders", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CourseWriteResult(CourseWriteStatus.Done, 0));

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service.UpdateAsync(Sessions.Teacher(1), 3, "Ders", null, null, Ct));

        // A topic is required, as for a new course; with one the change is stored and audited.
        _courses.Setup(c => c.UpdateAsync(3, 1, "Ders", "Konu", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CourseWriteResult(CourseWriteStatus.Done, 0));
        await Service.UpdateAsync(Sessions.Teacher(1), 3, "Ders", "Konu", null, Ct);
        Assert.Equal([(AuditEvent.CourseUpdated, (AccountRole?)AccountRole.Teacher)], _audit.Events);
    }

    [Fact]
    public async Task Delete_UsesTheClockAndTheConfirmedCount()
    {
        _courses.Setup(c => c.DeleteAsync(3, 1, 2, _clock.GetUtcNow().UtcDateTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CourseWriteResult(CourseWriteStatus.Done, 2));

        await Service.DeleteAsync(Sessions.Teacher(1), 3, 2, Ct);

        Assert.Equal([(AuditEvent.CourseDeleted, (AccountRole?)AccountRole.Teacher)], _audit.Events);
    }

    [Fact]
    public async Task Delete_StudentsChangedSinceTheConfirmation_AsksAgain()
    {
        _courses.Setup(c => c.DeleteAsync(3, 1, 0, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CourseWriteResult(CourseWriteStatus.StudentsChanged, 1));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.DeleteAsync(Sessions.Teacher(1), 3, 0, Ct));

        Assert.Equal(CourseService.StudentsChanged, error.Message);
        Assert.Empty(_audit.Events);
    }

    [Fact]
    public async Task Delete_ForeignMissingOrInvalidInput_SameAnswerAsMissing()
    {
        _courses.Setup(c => c.DeleteAsync(3, 2, 0, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CourseWriteResult(CourseWriteStatus.NotFound, 0));

        var foreign = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.DeleteAsync(Sessions.Teacher(2), 3, 0, Ct));
        var negative = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.DeleteAsync(Sessions.Teacher(2), 3, -1, Ct));
        var none = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.DeleteAsync(Sessions.Teacher(2), 0, 0, Ct));

        Assert.All(new[] { foreign, negative, none }, e => Assert.Equal(Access.CourseNotFound, e.Message));
        _courses.Verify(c => c.DeleteAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        await Assert.ThrowsAsync<AccessDeniedException>(() => Service.DeleteAsync(Sessions.Student(), 3, 0, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Service.UpdateAsync(Sessions.Student(), 3, "Ders", "Konu", null, Ct));
    }

    [Fact]
    public async Task WrongRole_IsDeniedAndAudited()
    {
        await Assert.ThrowsAsync<AccessDeniedException>(() => Service.CreateAsync(Sessions.Student(), "Ders", "Konu", null, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Service.AddNoteAsync(Sessions.Student(), 3, "Not", Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Service.GetTeachingAsync(Sessions.Student(), Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Service.GetCatalogAsync(Sessions.Teacher(), Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Service.GetDetailsAsync(null!, 3, Ct));

        Assert.Equal(4, _audit.Events.Count(e => e.Event == AuditEvent.AccessDenied));
        _courses.VerifyNoOtherCalls();
    }
}
