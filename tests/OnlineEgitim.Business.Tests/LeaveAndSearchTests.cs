using Microsoft.Extensions.Time.Testing;
using Moq;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Tests;

/// <summary>Leaving a course, the rule for joining it again and the catalogue search, with strict mocks.</summary>
public sealed class LeaveAndSearchTests
{
    // Monday 2 March 2026, 10:00 UTC; the fake clock's local time zone is UTC.
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTime Midnight = new(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IEnrollmentRepository> _enrollments = new(MockBehavior.Strict);
    private readonly Mock<ICourseRepository> _courses = new(MockBehavior.Strict);
    private readonly Mock<IAccountRepository> _accounts = new(MockBehavior.Strict);
    private readonly RecordingAudit _audit = new();
    private readonly FakeTimeProvider _clock = new(Now);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private EnrollmentService Enrollments => new(_enrollments.Object, _courses.Object, _clock, _audit);

    private CourseService Courses => new(_courses.Object, _enrollments.Object, _accounts.Object, _clock, _audit, Validators.Course, Validators.Note, Validators.CourseSearch);

    [Fact]
    public async Task Leave_ClosesTheSessionsOwnEnrollmentAtTheClocksTime()
    {
        _enrollments.Setup(e => e.LeaveAsync(5, 3, Now.UtcDateTime, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Enrollments.LeaveAsync(Sessions.Student(5), 3, Ct);

        Assert.Equal([(AuditEvent.EnrollmentLeft, (AccountRole?)AccountRole.Student)], _audit.Events);
    }

    [Fact]
    public async Task Leave_NotEnrolledNoSelectionOrWrongRole_IsRefused()
    {
        _enrollments.Setup(e => e.LeaveAsync(5, 3, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var notEnrolled = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.LeaveAsync(Sessions.Student(5), 3, Ct));
        var none = await Assert.ThrowsAsync<BusinessRuleException>(() => Enrollments.LeaveAsync(Sessions.Student(5), 0, Ct));
        await Assert.ThrowsAsync<AccessDeniedException>(() => Enrollments.LeaveAsync(Sessions.Teacher(5), 3, Ct));

        Assert.Equal(EnrollmentService.NotEnrolled, notEnrolled.Message);
        Assert.Equal(EnrollmentService.CourseRequired, none.Message);
        Assert.Equal([(AuditEvent.AccessDenied, (AccountRole?)AccountRole.Teacher)], _audit.Events);
        _enrollments.Verify(e => e.LeaveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>The rejoin rule is "not on the day of leaving": the boundary given to the repository is the last local midnight.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(23, 59)]
    public async Task Enroll_RejoinBoundaryIsTheLastLocalMidnight(int hour, int minute)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 2, hour, minute, 0, TimeSpan.Zero));
        _enrollments.Setup(e => e.AddAsync(5, 3, Midnight, It.IsAny<CancellationToken>())).ReturnsAsync(EnrollmentStatus.RejoinTooSoon);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new EnrollmentService(_enrollments.Object, _courses.Object, clock, _audit).EnrollAsync(Sessions.Student(5), 3, Ct));

        Assert.Equal(EnrollmentService.RejoinTooSoon, error.Message);
        Assert.Empty(_audit.Events);
    }

    [Fact]
    public async Task Search_CleansTheTextsAndTurnsThePageIntoSkipAndTake()
    {
        CourseSearchCriteria? asked = null;
        _courses.Setup(c => c.SearchAsync(It.IsAny<CourseSearchCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<CourseSearchCriteria, CancellationToken>((criteria, _) => asked = criteria)
            .ReturnsAsync(new CatalogPage([new CatalogEntry(new CourseSummary(3, "Ders", 1, "Deniz Örnek", null), true)], 45));

        var result = await Courses.SearchCatalogAsync(
            Sessions.Student(5),
            new CourseSearch(" prog ", "  ", " Deniz ", true, EnrollmentFilter.NotEnrolled, 2, 20),
            Ct);

        Assert.Equal(new CourseSearchCriteria(5, "prog", null, "Deniz", true, EnrollmentFilter.NotEnrolled, 40, 20), asked);
        Assert.Equal((2, 3, 45), (result.PageIndex, result.PageCount, result.TotalCount));
        Assert.True(Assert.Single(result.Items).Enrolled);
    }

    [Fact]
    public async Task Search_PagePastTheEnd_ReturnsTheLastPage()
    {
        _courses.Setup(c => c.SearchAsync(It.Is<CourseSearchCriteria>(s => s.Skip == 100), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogPage([], 41));
        _courses.Setup(c => c.SearchAsync(It.Is<CourseSearchCriteria>(s => s.Skip == 40), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogPage([new CatalogEntry(new CourseSummary(41, "Son", 1, "Deniz Örnek", null), false)], 41));

        var result = await Courses.SearchCatalogAsync(Sessions.Student(5), new CourseSearch(PageIndex: 5), Ct);

        Assert.Equal(2, result.PageIndex);
        Assert.Equal(41, Assert.Single(result.Items).Course.Id);
    }

    public static TheoryData<CourseSearch> InvalidSearches => new()
    {
        new CourseSearch(PageIndex: -1),
        new CourseSearch(PageIndex: CourseSearchValidator.MaxPageIndex + 1),
        new CourseSearch(PageSize: 0),
        new CourseSearch(PageSize: CourseSearch.MaxPageSize + 1),
        new CourseSearch(Name: new string('a', InputRules.CourseNameMaxLength + 1)),
        new CourseSearch(Topic: new string('a', InputRules.TopicMaxLength + 1)),
        new CourseSearch(TeacherName: new string('a', InputRules.PersonNameMaxLength + 1)),
        new CourseSearch(Enrollment: (EnrollmentFilter)7),
    };

    [Theory]
    [MemberData(nameof(InvalidSearches))]
    public async Task Search_InvalidCriteria_AreRefusedBeforeTheDatabase(CourseSearch search)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Courses.SearchCatalogAsync(Sessions.Student(5), search, Ct));

        Assert.Equal(ValidationMessages.SearchInvalid, error.Message);
        _courses.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Search_BoundaryValues_AreAccepted()
    {
        _courses.Setup(c => c.SearchAsync(It.IsAny<CourseSearchCriteria>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CatalogPage([], 0));

        await Courses.SearchCatalogAsync(Sessions.Student(5), new CourseSearch(Name: new string('a', InputRules.CourseNameMaxLength), PageSize: 1), Ct);
        var result = await Courses.SearchCatalogAsync(Sessions.Student(5), new CourseSearch(PageIndex: 3, PageSize: CourseSearch.MaxPageSize), Ct);

        Assert.Equal((0, 1), (result.PageIndex, result.PageCount)); // nothing matches: the first page, no second query
        await Assert.ThrowsAsync<AccessDeniedException>(() => Courses.SearchCatalogAsync(Sessions.Teacher(), new CourseSearch(), Ct));
    }
}
