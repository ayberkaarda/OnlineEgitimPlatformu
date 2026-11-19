using Microsoft.Extensions.DependencyInjection;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Services;
using static OnlineEgitim.Data.Tests.TestServices;

namespace OnlineEgitim.Data.Tests;

/// <summary>
/// Leaving and searching on a real SQL Server instance (see <see cref="SqlServerTestDatabase"/>), where
/// LIKE treats "[" as a wildcard too and the Turkish collation ignores case.
/// </summary>
public sealed class SqlServerLeaveAndSearchTests
{
    [Fact]
    public async Task Search_EscapesEverySqlServerWildcardAndIgnoresCase()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var provider = await Race.InitializeAsync(database, ct);
        var courses = provider.GetRequiredService<ICourseService>();
        foreach (var name in new[] { "C[#] Dili", "C# Dili", "Yüzde 100% Pratik", "Yüzde 100 Pratik", "a_b Dersi", "axb Dersi", "İLERİ Seviye" })
        {
            await courses.CreateAsync(Teacher(DenizId), name, "Konu", null, ct);
        }

        async Task<string[]> NamesAsync(string text) =>
            (await courses.SearchCatalogAsync(Student(MertId), new CourseSearch(Name: text), ct)).Items.Select(i => i.Course.Name).ToArray();

        Assert.Equal(["C[#] Dili"], await NamesAsync("[#]"));
        Assert.Equal(["C[#] Dili"], await NamesAsync("["));
        Assert.Equal(["Yüzde 100% Pratik"], await NamesAsync("0%"));
        Assert.Equal(["a_b Dersi"], await NamesAsync("a_b"));
        Assert.Equal(["İLERİ Seviye"], await NamesAsync("ileri"));
    }

    /// <summary>
    /// The only student of a one-seat course leaves while another student tries to join: the join waits
    /// for the course's lock and then either finds the seat taken (it ran first) or free. The course never
    /// has more students than seats.
    /// </summary>
    [Fact]
    public async Task Leave_RacingAJoinForTheSameSeat_NeverOverfillsTheCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await using var provider = await Race.InitializeAsync(database, ct);
        var courses = provider.GetRequiredService<ICourseService>();
        var enrollments = provider.GetRequiredService<IEnrollmentService>();
        var students = await Race.AddStudentsAsync(database, 8, ct);

        for (var round = 0; round < 4; round++)
        {
            var leaver = students[2 * round];
            var joiner = students[(2 * round) + 1];
            var courseId = await courses.CreateAsync(Teacher(DenizId), "Tek Koltuk " + round, "Konu", 1, ct);
            await enrollments.EnrollAsync(Student(leaver), courseId, ct);

            var results = await Race.RunAsync(
                2,
                slot => slot == 0 ? enrollments.LeaveAsync(Student(leaver), courseId, ct) : enrollments.EnrollAsync(Student(joiner), courseId, ct),
                ct);

            Assert.Null(results[0]);
            var active = await database.ScalarAsync("SELECT COUNT(*) FROM dbo.OgrenciKurslar WHERE DersID = @id AND AyrilmaTarihi IS NULL", ct, ("@id", courseId));
            if (results[1] is null)
            {
                Assert.Equal(1, active);
            }
            else
            {
                Assert.Equal(EnrollmentService.CourseFull, Assert.IsType<BusinessRuleException>(results[1]).Message);
                Assert.Equal(0, active);
            }
        }
    }
}
