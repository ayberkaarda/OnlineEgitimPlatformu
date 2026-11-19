using Microsoft.EntityFrameworkCore;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Domain.Entities;
using static OnlineEgitim.Data.Tests.TestServices;

namespace OnlineEgitim.Data.Tests;

/// <summary>
/// Leaving a course, joining it again and the catalogue search on SQLite in-memory. The test clock
/// starts on Monday 2 March 2026, 10:10, and its local time zone is UTC.
/// </summary>
public sealed class LeaveAndSearchTests : IAsyncLifetime
{
    private readonly SqliteDatabase _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ICourseService Courses => _db.Get<ICourseService>();

    private IEnrollmentService Enrollments => _db.Get<IEnrollmentService>();

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    /// <summary>Leaving frees the seat at once and closes the notes and the course to the student who left.</summary>
    [Fact]
    public async Task Leave_FreesTheSeatAndClosesTheNotes()
    {
        var id = await Courses.CreateAsync(Teacher(DenizId), "Tek Kişilik", "Konu", 1, Ct);
        await Courses.AddNoteAsync(Teacher(DenizId), id, "Gizli not", Ct);
        await Enrollments.EnrollAsync(Student(MertId), id, Ct);
        Assert.True((await Courses.GetDetailsAsync(Student(MertId), id, Ct)).NotesVisible);
        var full = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.EnrollAsync(Student(SelinId), id, Ct));
        Assert.Equal(EnrollmentService.CourseFull, full.Message);

        await Enrollments.LeaveAsync(Student(MertId), id, Ct);

        await Enrollments.EnrollAsync(Student(SelinId), id, Ct);
        var details = await Courses.GetDetailsAsync(Student(MertId), id, Ct);
        Assert.False(details.NotesVisible);
        Assert.Empty(details.Notes);
        Assert.Equal(1, details.StudentCount);
        Assert.Empty(await Enrollments.GetMyCoursesAsync(Student(MertId), Ct));
        Assert.Equal(["Selin Deneme"], await Enrollments.GetStudentsAsync(Teacher(DenizId), id, Ct));
    }

    [Fact]
    public async Task Leave_WhenNotEnrolledTwiceOrFromADeletedCourse_IsRefused()
    {
        var never = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.LeaveAsync(Student(MertId), DenizCourseId, Ct));
        await Enrollments.EnrollAsync(Student(MertId), DenizCourseId, Ct);
        await Enrollments.EnrollAsync(Student(SelinId), DenizCourseId, Ct);
        await Enrollments.LeaveAsync(Student(MertId), DenizCourseId, Ct);
        var twice = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.LeaveAsync(Student(MertId), DenizCourseId, Ct));
        await Courses.DeleteAsync(Teacher(DenizId), DenizCourseId, 1, Ct);
        var deleted = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.LeaveAsync(Student(SelinId), DenizCourseId, Ct));

        Assert.All(new[] { never, twice, deleted }, e => Assert.Equal(EnrollmentService.NotEnrolled, e.Message));
        await using var context = _db.CreateContext();
        var mert = await context.Enrollments.SingleAsync(e => e.StudentId == MertId, Ct);
        Assert.Equal(_db.Clock.GetUtcNow().UtcDateTime, mert.LeftUtc);
    }

    /// <summary>
    /// Joining again is refused on the day of leaving (up to 23:59) and allowed from the next midnight
    /// on; it reopens the same row and still needs a free seat.
    /// </summary>
    [Fact]
    public async Task Rejoin_SameDayRefusedNextDayAllowedWithinTheSeats()
    {
        var id = await Courses.CreateAsync(Teacher(DenizId), "Tek Kişilik", "Konu", 1, Ct);
        await Enrollments.EnrollAsync(Student(MertId), id, Ct);
        await Enrollments.LeaveAsync(Student(MertId), id, Ct);

        var sameDay = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.EnrollAsync(Student(MertId), id, Ct));
        _db.Clock.SetUtcNow(new DateTimeOffset(2026, 3, 2, 23, 59, 59, TimeSpan.Zero));
        var lastMinute = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.EnrollAsync(Student(MertId), id, Ct));
        Assert.Equal(EnrollmentService.RejoinTooSoon, sameDay.Message);
        Assert.Equal(EnrollmentService.RejoinTooSoon, lastMinute.Message);

        // Selin takes the seat; the next day Mert may join again, but the course is full.
        await Enrollments.EnrollAsync(Student(SelinId), id, Ct);
        _db.Clock.SetUtcNow(new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero));
        var full = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.EnrollAsync(Student(MertId), id, Ct));
        Assert.Equal(EnrollmentService.CourseFull, full.Message);

        await Enrollments.LeaveAsync(Student(SelinId), id, Ct);
        await Enrollments.EnrollAsync(Student(MertId), id, Ct);
        await using var context = _db.CreateContext();
        Assert.Equal(1, await context.Enrollments.CountAsync(e => e.StudentId == MertId && e.CourseId == id, Ct));
        Assert.Null((await context.Enrollments.SingleAsync(e => e.StudentId == MertId && e.CourseId == id, Ct)).LeftUtc);
        Assert.True((await Courses.GetDetailsAsync(Student(MertId), id, Ct)).NotesVisible);
    }

    /// <summary>Wildcards are matched literally: "%" and "_" find only names that contain them.</summary>
    [Fact]
    public async Task Search_WildcardsMatchLiterally()
    {
        foreach (var name in new[] { "Yüzde 100% Pratik", "Yüzde 100 Pratik", "a_b Dersi", "axb Dersi", "C[#] Dili", "C# Dili", "Ters\\Bölü" })
        {
            await Courses.CreateAsync(Teacher(DenizId), name, "Konu", null, Ct);
        }

        Assert.Equal(["Yüzde 100% Pratik"], await NamesAsync(new CourseSearch(Name: "%")));
        Assert.Equal(["a_b Dersi"], await NamesAsync(new CourseSearch(Name: "_")));
        Assert.Equal(["C[#] Dili"], await NamesAsync(new CourseSearch(Name: "[#]")));
        Assert.Equal(["Ters\\Bölü"], await NamesAsync(new CourseSearch(Name: "\\")));
        Assert.Equal(["C# Dili", "C[#] Dili"], await NamesAsync(new CourseSearch(Name: " Dili ")));
    }

    [Fact]
    public async Task Search_FiltersByTopicTeacherSeatsAndEnrollment()
    {
        var full = await Courses.CreateAsync(Teacher(EceId), "Dolu Ders", "Renkler", 1, Ct);
        await Enrollments.EnrollAsync(Student(SelinId), full, Ct);
        await Enrollments.EnrollAsync(Student(MertId), DenizCourseId, Ct);

        Assert.Equal(["Dolu Ders"], await NamesAsync(new CourseSearch(Topic: "renk")));
        Assert.Equal(["Dolu Ders", "Web Tasarımı"], await NamesAsync(new CourseSearch(TeacherName: "Ece")));
        Assert.Equal(["Temel Programlama", "Web Tasarımı"], await NamesAsync(new CourseSearch(OnlyWithFreeSeats: true)));
        Assert.Equal(["Temel Programlama"], await NamesAsync(new CourseSearch(Enrollment: EnrollmentFilter.Enrolled)));
        Assert.Equal(["Dolu Ders", "Web Tasarımı"], await NamesAsync(new CourseSearch(Enrollment: EnrollmentFilter.NotEnrolled)));
        Assert.Equal(["Dolu Ders"], await NamesAsync(new CourseSearch(Enrollment: EnrollmentFilter.Enrolled), SelinId));

        var page = await Courses.SearchCatalogAsync(Student(MertId), new CourseSearch(), Ct);
        Assert.Equal([false, true, false], page.Items.Select(i => i.Enrolled));
        Assert.Equal([1, 1, 0], page.Items.Select(i => i.Course.StudentCount));
    }

    /// <summary>
    /// Twenty-five courses with the same name split into pages of twenty and five, in id order: every
    /// course appears once, and a page past the end shows the last page.
    /// </summary>
    [Fact]
    public async Task Search_PagesAreStableForEqualNames()
    {
        var ids = new List<int>();
        for (var i = 0; i < 25; i++)
        {
            ids.Add(await Courses.CreateAsync(Teacher(i % 2 == 0 ? DenizId : EceId), "Aynı Ad", "Konu", null, Ct));
        }

        var first = await Courses.SearchCatalogAsync(Student(MertId), new CourseSearch(Name: "Aynı"), Ct);
        var second = await Courses.SearchCatalogAsync(Student(MertId), new CourseSearch(Name: "Aynı", PageIndex: 1), Ct);
        var beyond = await Courses.SearchCatalogAsync(Student(MertId), new CourseSearch(Name: "Aynı", PageIndex: 9), Ct);

        Assert.Equal((25, 2, 20, 5), (first.TotalCount, first.PageCount, first.Items.Count, second.Items.Count));
        Assert.Equal(ids, first.Items.Concat(second.Items).Select(i => i.Course.Id));
        Assert.Equal(1, beyond.PageIndex);
        Assert.Equal(second.Items.Select(i => i.Course.Id), beyond.Items.Select(i => i.Course.Id));
    }

    [Fact]
    public async Task Search_HidesDeletedCourses()
    {
        await Courses.DeleteAsync(Teacher(DenizId), DenizCourseId, 0, Ct);

        Assert.Equal(["Web Tasarımı"], await NamesAsync(new CourseSearch()));
    }

    private async Task<IReadOnlyList<string>> NamesAsync(CourseSearch search, int studentId = MertId) =>
        (await Courses.SearchCatalogAsync(Student(studentId), search, Ct)).Items.Select(i => i.Course.Name).ToList();
}
