using Microsoft.EntityFrameworkCore;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using static OnlineEgitim.Data.Tests.TestServices;

namespace OnlineEgitim.Data.Tests;

/// <summary>
/// A teacher's own courses on SQLite in-memory: editing, the capacity and its limits (0, 1, full,
/// lowered), deleting with and without students, and the same answer for another teacher's course.
/// </summary>
public sealed class CourseManagementTests : IAsyncLifetime
{
    private const string NotFound = "Ders bulunamadı.";

    private readonly SqliteDatabase _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ICourseService Courses => _db.Get<ICourseService>();

    private IEnrollmentService Enrollments => _db.Get<IEnrollmentService>();

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Capacity_Zero_TakesNobody()
    {
        var id = await Courses.CreateAsync(Teacher(DenizId), "Kapalı Ders", "Konu", 0, Ct);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.EnrollAsync(Student(MertId), id, Ct));

        Assert.Equal(EnrollmentService.CourseFull, error.Message);
        Assert.Equal(0, await CountRowsAsync(id));
    }

    [Fact]
    public async Task Capacity_One_TakesOneStudentAndRefusesTheNext()
    {
        var id = await Courses.CreateAsync(Teacher(DenizId), "Tek Kişilik", "Konu", 1, Ct);

        await Enrollments.EnrollAsync(Student(MertId), id, Ct);
        var full = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.EnrollAsync(Student(SelinId), id, Ct));
        var again = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.EnrollAsync(Student(MertId), id, Ct));

        Assert.Equal(EnrollmentService.CourseFull, full.Message);
        Assert.Equal(EnrollmentService.AlreadyEnrolled, again.Message);
        var listed = (await Courses.GetTeachingAsync(Teacher(DenizId), Ct)).Single(c => c.Id == id);
        Assert.Equal((1, 1, false), (listed.Capacity!.Value, listed.StudentCount, listed.HasFreeSeat));
    }

    [Fact]
    public async Task Update_CapacityCannotGoBelowTheStudentsButMayEqualThem()
    {
        await Enrollments.EnrollAsync(Student(MertId), DenizCourseId, Ct);
        await Enrollments.EnrollAsync(Student(SelinId), DenizCourseId, Ct);

        var below = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Courses.UpdateAsync(Teacher(DenizId), DenizCourseId, "Yeni Ad", "Yeni Konu", 1, Ct));
        Assert.Equal("Kontenjan, kayıtlı öğrenci sayısının (2) altına düşürülemez.", below.Message);
        Assert.Equal(("Temel Programlama", (int?)null), await CourseStateAsync(DenizCourseId));

        await Courses.UpdateAsync(Teacher(DenizId), DenizCourseId, " Yeni Ad ", " Yeni Konu ", 2, Ct);
        Assert.Equal(("Yeni Ad", (int?)2), await CourseStateAsync(DenizCourseId));
        var details = await Courses.GetDetailsAsync(Teacher(DenizId), DenizCourseId, Ct);
        Assert.Equal(("Yeni Konu", (int?)2, 2), (details.Topic, details.Capacity, details.StudentCount));

        // Full now; back to no limit opens it again.
        await AddStudentAsync("Üçüncü Öğrenci");
        var full = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.EnrollAsync(Student(3), DenizCourseId, Ct));
        Assert.Equal(EnrollmentService.CourseFull, full.Message);
        await Courses.UpdateAsync(Teacher(DenizId), DenizCourseId, "Yeni Ad", "Yeni Konu", null, Ct);
        await Enrollments.EnrollAsync(Student(3), DenizCourseId, Ct);
        Assert.Equal(3, await CountRowsAsync(DenizCourseId));
    }

    /// <summary>IDOR: another teacher's course cannot be changed or deleted and looks exactly like a missing one.</summary>
    [Fact]
    public async Task ForeignCourse_UpdateAndDelete_LookLikeAMissingCourseAndChangeNothing()
    {
        await Enrollments.EnrollAsync(Student(MertId), DenizCourseId, Ct);
        var ece = Teacher(EceId);

        var errors = new[]
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => Courses.UpdateAsync(ece, DenizCourseId, "Ele Geçir", "Konu", 0, Ct)),
            await Assert.ThrowsAsync<BusinessRuleException>(() => Courses.UpdateAsync(ece, 999, "Ele Geçir", "Konu", 0, Ct)),
            await Assert.ThrowsAsync<BusinessRuleException>(() => Courses.DeleteAsync(ece, DenizCourseId, 1, Ct)),
            await Assert.ThrowsAsync<BusinessRuleException>(() => Courses.DeleteAsync(ece, 999, 1, Ct)),
        };

        Assert.All(errors, e => Assert.Equal(NotFound, e.Message));
        Assert.Equal(("Temel Programlama", (int?)null), await CourseStateAsync(DenizCourseId));
        await using var context = _db.CreateContext();
        Assert.False(await context.Courses.AnyAsync(c => c.DeletedUtc != null, Ct));
        Assert.Equal(1, await context.Enrollments.CountAsync(e => e.LeftUtc == null, Ct));
    }

    /// <summary>
    /// Deleting a course with students needs the confirmed number: a different number deletes nothing.
    /// The deletion keeps the rows (the course with its notes, the enrollments closed), and the course
    /// disappears from every list and view, for its teacher and its former students alike.
    /// </summary>
    [Fact]
    public async Task Delete_WithStudents_NeedsTheConfirmedCountAndClosesTheEnrollments()
    {
        await Courses.AddNoteAsync(Teacher(DenizId), DenizCourseId, "Ders notu", Ct);
        await Enrollments.EnrollAsync(Student(MertId), DenizCourseId, Ct);
        await Enrollments.EnrollAsync(Student(SelinId), DenizCourseId, Ct);

        var stale = await Assert.ThrowsAsync<BusinessRuleException>(() => Courses.DeleteAsync(Teacher(DenizId), DenizCourseId, 1, Ct));
        Assert.Equal(CourseService.StudentsChanged, stale.Message);
        Assert.Equal(2, await CountActiveAsync(DenizCourseId));

        await Courses.DeleteAsync(Teacher(DenizId), DenizCourseId, 2, Ct);

        Assert.DoesNotContain(await Courses.GetTeachingAsync(Teacher(DenizId), Ct), c => c.Id == DenizCourseId);
        Assert.DoesNotContain(await Courses.GetCatalogAsync(Student(MertId), Ct), c => c.Id == DenizCourseId);
        Assert.Empty(await Enrollments.GetMyCoursesAsync(Student(MertId), Ct));
        var asStudent = await Assert.ThrowsAsync<BusinessRuleException>(() => Courses.GetDetailsAsync(Student(MertId), DenizCourseId, Ct));
        var asTeacher = await Assert.ThrowsAsync<BusinessRuleException>(() => Courses.GetDetailsAsync(Teacher(DenizId), DenizCourseId, Ct));
        var students = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.GetStudentsAsync(Teacher(DenizId), DenizCourseId, Ct));
        var note = await Assert.ThrowsAsync<BusinessRuleException>(() => Courses.AddNoteAsync(Teacher(DenizId), DenizCourseId, "Geç not", Ct));
        var join = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.EnrollAsync(Student(MertId), DenizCourseId, Ct));
        var twice = await Assert.ThrowsAsync<BusinessRuleException>(() => Courses.DeleteAsync(Teacher(DenizId), DenizCourseId, 0, Ct));
        Assert.All(new[] { asStudent, asTeacher, students, note, join, twice }, e => Assert.Equal(NotFound, e.Message));

        await using var context = _db.CreateContext();
        var course = await context.Courses.SingleAsync(c => c.Id == DenizCourseId, Ct);
        Assert.Equal(_db.Clock.GetUtcNow().UtcDateTime, course.DeletedUtc);
        Assert.Equal(2, await context.Enrollments.CountAsync(e => e.CourseId == DenizCourseId && e.LeftUtc == course.DeletedUtc, Ct));
        Assert.Equal(1, await context.Notes.CountAsync(n => n.CourseId == DenizCourseId, Ct));
    }

    [Fact]
    public async Task Delete_EmptyCourse_NeedsNoStudentsAndLeavesOtherCoursesAlone()
    {
        await Enrollments.EnrollAsync(Student(MertId), EceCourseId, Ct);

        await Courses.DeleteAsync(Teacher(DenizId), DenizCourseId, 0, Ct);

        Assert.Equal([EceCourseId], (await Courses.GetCatalogAsync(Student(SelinId), Ct)).Select(c => c.Id));
        Assert.Equal([EceCourseId], (await Enrollments.GetMyCoursesAsync(Student(MertId), Ct)).Select(c => c.Id));
        Assert.Equal(1, await CountActiveAsync(EceCourseId));
    }

    [Fact]
    public async Task InvalidCapacity_WritesNothing()
    {
        var create = await Assert.ThrowsAsync<BusinessRuleException>(() => Courses.CreateAsync(Teacher(DenizId), "Ders", "Konu", -1, Ct));
        var update = await Assert.ThrowsAsync<BusinessRuleException>(() => Courses.UpdateAsync(Teacher(DenizId), DenizCourseId, "Ders", "Konu", 1001, Ct));

        Assert.Equal(ValidationMessages.CapacityInvalid, create.Message);
        Assert.Equal(ValidationMessages.CapacityInvalid, update.Message);
        await using var context = _db.CreateContext();
        Assert.Equal(2, await context.Courses.CountAsync(Ct));
        Assert.Equal(("Temel Programlama", (int?)null), await CourseStateAsync(DenizCourseId));
    }

    private async Task<(string Name, int? Capacity)> CourseStateAsync(int id)
    {
        await using var context = _db.CreateContext();
        var course = await context.Courses.SingleAsync(c => c.Id == id, Ct);
        return (course.Name, course.Capacity);
    }

    private async Task<int> CountRowsAsync(int courseId)
    {
        await using var context = _db.CreateContext();
        return await context.Enrollments.CountAsync(e => e.CourseId == courseId, Ct);
    }

    private async Task<int> CountActiveAsync(int courseId)
    {
        await using var context = _db.CreateContext();
        return await context.Enrollments.CountAsync(e => e.CourseId == courseId && e.LeftUtc == null, Ct);
    }

    private async Task AddStudentAsync(string name)
    {
        await using var context = _db.CreateContext();
        context.Students.Add(new Domain.Entities.Student { FullName = name, Email = Guid.NewGuid().ToString("N") + "@example.com", PasswordHash = "-" });
        await context.SaveChangesAsync(Ct);
    }
}
