using Microsoft.Extensions.DependencyInjection;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Services;
using static OnlineEgitim.Data.Tests.TestServices;

namespace OnlineEgitim.Data.Tests;

/// <summary>
/// Seats under concurrency on a real SQL Server instance (see <see cref="SqlServerTestDatabase"/>): the
/// course's application lock decides who gets the last seat, whether a capacity can be lowered and
/// whether a deletion or an enrollment wins.
/// </summary>
public sealed class SqlServerCourseTests
{
    private const int Rounds = 4;

    /// <summary>Two students race for the last seat of a course: exactly one joins, the other is told it is full.</summary>
    [Fact]
    public async Task Capacity_TwoStudentsRaceForTheLastSeat_ExactlyOneJoins()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var provider = await Race.InitializeAsync(database, ct);
        var courses = provider.GetRequiredService<ICourseService>();
        var enrollments = provider.GetRequiredService<IEnrollmentService>();
        var students = await Race.AddStudentsAsync(database, 2 * Rounds, ct);

        for (var round = 0; round < Rounds; round++)
        {
            // Two seats, one taken: one seat is left.
            var courseId = await courses.CreateAsync(Teacher(DenizId), "Son Koltuk " + round, "Konu", 2, ct);
            await enrollments.EnrollAsync(Student(MertId), courseId, ct);
            var racers = new[] { students[2 * round], students[(2 * round) + 1] };

            var results = await Race.RunAsync(2, slot => enrollments.EnrollAsync(Student(racers[slot]), courseId, ct), ct);

            Assert.Single(results, r => r is null);
            Assert.Equal(EnrollmentService.CourseFull, Assert.IsType<BusinessRuleException>(Assert.Single(results, r => r is not null)).Message);
            Assert.Equal(2, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.OgrenciKurslar WHERE DersID = @id AND AyrilmaTarihi IS NULL", ct, ("@id", courseId)));
        }
    }

    /// <summary>
    /// Many students race into a small course: never more students than seats, and every refused
    /// student is told the course is full.
    /// </summary>
    [Fact]
    public async Task Capacity_ManyStudentsRace_NeverMoreThanTheSeats()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var provider = await Race.InitializeAsync(database, ct);
        var courseId = await provider.GetRequiredService<ICourseService>().CreateAsync(Teacher(DenizId), "Küçük Sınıf", "Konu", 3, ct);
        var enrollments = provider.GetRequiredService<IEnrollmentService>();
        var students = await Race.AddStudentsAsync(database, 12, ct);

        var results = await Race.RunAsync(students.Length, slot => enrollments.EnrollAsync(Student(students[slot]), courseId, ct), ct);

        Assert.Equal(3, results.Count(r => r is null));
        Assert.All(results.Where(r => r is not null), r => Assert.Equal(EnrollmentService.CourseFull, Assert.IsType<BusinessRuleException>(r).Message));
        Assert.Equal(3, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.OgrenciKurslar WHERE DersID = @id", ct, ("@id", courseId)));
    }

    /// <summary>
    /// The teacher lowers the capacity to the current number of students while two more join: either
    /// the change waits and is refused because a student joined, or it wins and the students are
    /// refused. The stored capacity is never below the number of students.
    /// </summary>
    [Fact]
    public async Task Capacity_LoweredWhileStudentsJoin_NeverBelowTheStudents()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var provider = await Race.InitializeAsync(database, ct);
        var courses = provider.GetRequiredService<ICourseService>();
        var enrollments = provider.GetRequiredService<IEnrollmentService>();
        var students = await Race.AddStudentsAsync(database, 2 * Rounds, ct);

        for (var round = 0; round < Rounds; round++)
        {
            var courseId = await courses.CreateAsync(Teacher(DenizId), "Daralan " + round, "Konu", 5, ct);
            await enrollments.EnrollAsync(Student(MertId), courseId, ct);

            var results = await Race.RunAsync(
                3,
                slot => slot == 0
                    ? courses.UpdateAsync(Teacher(DenizId), courseId, "Daralan " + round, "Konu", 1, ct)
                    : enrollments.EnrollAsync(Student(students[(2 * round) + slot - 1]), courseId, ct),
                ct);

            var capacity = await database.ScalarAsync("SELECT Kontenjan FROM dbo.Dersler WHERE DersID = @id", ct, ("@id", courseId));
            var active = await database.ScalarAsync("SELECT COUNT(*) FROM dbo.OgrenciKurslar WHERE DersID = @id AND AyrilmaTarihi IS NULL", ct, ("@id", courseId));
            Assert.True(active <= capacity, $"{active} students in a course of {capacity} seats.");
            if (results[0] is null)
            {
                Assert.Equal((1, 1), (capacity, active));
                Assert.All(results.Skip(1), r => Assert.Equal(EnrollmentService.CourseFull, Assert.IsType<BusinessRuleException>(r).Message));
            }
            else
            {
                Assert.StartsWith("Kontenjan, kayıtlı öğrenci sayısının", Assert.IsType<BusinessRuleException>(results[0]).Message, StringComparison.Ordinal);
                Assert.Equal(5, capacity);
            }
        }
    }

    /// <summary>
    /// A course lock held by another instance makes an enrollment wait for the whole lock timeout and then
    /// end as "busy" (the lock was refused), never as a command timeout, even when the configured command
    /// timeout is shorter than the lock wait. Nothing is written; once the lock is free the enrollment works.
    /// </summary>
    [Fact]
    public async Task CourseLock_HeldByAnotherSession_ReportsTheLockMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        int courseId;
        await using (var setup = await Race.InitializeAsync(database, ct))
        {
            courseId = await setup.GetRequiredService<ICourseService>().CreateAsync(Teacher(DenizId), "Kilitli Ders", "Konu", 5, ct);
        }

        await using var provider = database.BuildServicesWithCommandTimeout(5);
        var enrollments = provider.GetRequiredService<IEnrollmentService>();

        var waited = System.Diagnostics.Stopwatch.StartNew();
        await using (await database.HoldAppLockAsync(Persistence.CourseLock.Resource(courseId), ct))
        {
            await Assert.ThrowsAsync<Persistence.AppLockNotGrantedException>(() => enrollments.EnrollAsync(Student(MertId), courseId, ct));
        }

        Assert.True(waited.Elapsed >= TimeSpan.FromSeconds(25), "The enrollment did not wait for the course lock.");
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.OgrenciKurslar WHERE DersID = @id", ct, ("@id", courseId)));
        await enrollments.EnrollAsync(Student(MertId), courseId, ct);
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.OgrenciKurslar WHERE DersID = @id AND AyrilmaTarihi IS NULL", ct, ("@id", courseId)));
    }

    /// <summary>
    /// The teacher deletes an empty course (confirmed for no students) while a student joins it: one of
    /// the two wins. A deleted course never keeps an active enrollment, and a refused deletion leaves
    /// the student in the course.
    /// </summary>
    [Fact]
    public async Task Delete_RacingAnEnrollment_NeverLeavesAStudentInADeletedCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var provider = await Race.InitializeAsync(database, ct);
        var courses = provider.GetRequiredService<ICourseService>();
        var enrollments = provider.GetRequiredService<IEnrollmentService>();

        for (var round = 0; round < Rounds; round++)
        {
            var courseId = await courses.CreateAsync(Teacher(DenizId), "Silinen " + round, "Konu", null, ct);

            var results = await Race.RunAsync(
                2,
                slot => slot == 0
                    ? courses.DeleteAsync(Teacher(DenizId), courseId, 0, ct)
                    : enrollments.EnrollAsync(Student(MertId), courseId, ct),
                ct);

            var deleted = await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Dersler WHERE DersID = @id AND SilinmeTarihi IS NOT NULL", ct, ("@id", courseId));
            var active = await database.ScalarAsync("SELECT COUNT(*) FROM dbo.OgrenciKurslar WHERE DersID = @id AND AyrilmaTarihi IS NULL", ct, ("@id", courseId));
            Assert.Single(results, r => r is null);
            if (results[0] is null)
            {
                Assert.Equal((1, 0), (deleted, active));
                Assert.Equal("Ders bulunamadı.", Assert.IsType<BusinessRuleException>(results[1]).Message);
            }
            else
            {
                Assert.Equal((0, 1), (deleted, active));
                Assert.Equal(CourseService.StudentsChanged, Assert.IsType<BusinessRuleException>(results[0]).Message);
            }
        }
    }
}
