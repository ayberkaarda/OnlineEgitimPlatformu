using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Tests.Support;
using OnlineEgitim.UI.Theming;
using static OnlineEgitim.UI.Tests.Support.StaThread;

namespace OnlineEgitim.UI.Tests;

/// <summary>
/// The panels keep every control inside its parent, without overlaps, on a small laptop, a common laptop
/// and a large screen at four text sizes; long course and person names never hide seats or a status.
/// Nothing is shown.
/// </summary>
public sealed class LayoutTests
{
    [Fact]
    public void StudentPanel_FitsEveryScreen() => Run(() =>
        LayoutAssert.FitsInsideOnEveryScreen(() => TestForms.StudentPanel(LongCatalog())));

    [Fact]
    public void TeacherPanel_FitsEveryScreen() => Run(() =>
        LayoutAssert.FitsInsideOnEveryScreen(() => TestForms.TeacherPanel()));

    [Fact]
    public void AdministratorPanel_FitsEveryScreen() => Run(() =>
        LayoutAssert.FitsInsideOnEveryScreen(() => TestForms.AdminPanel()));

    [Fact]
    public void SignIn_FitsEveryScreen() => Run(() =>
        LayoutAssert.FitsInsideOnEveryScreen(() => new GirisForm(new PendingAuthService(), new NoFormsFactory()) { Stage = SetupStage.FirstAdminNeedsKey }));

    [Fact]
    public void CourseDetails_FitEveryScreen() => Run(() =>
        LayoutAssert.FitsInsideOnEveryScreen(TestForms.CourseDetails));

    /// <summary>
    /// A course with a very long name and teacher: the row keeps its status pill (own enrollment, full) at full
    /// width beside the name, and the seats lead the second line, so neither the name nor the teacher hides them.
    /// </summary>
    [Fact]
    public void LongCourseNames_KeepTheSeatsAndTheStatus()
    {
        var course = LongCatalog().Courses[0];

        var enrolled = new CourseItem(course, CourseItemStyle.Catalog, Enrolled: true).Row();
        var full = new CourseItem(course with { StudentCount = 12 }, CourseItemStyle.Catalog).Row();
        var teaching = new CourseItem(course, CourseItemStyle.Teaching).Row();

        Assert.Equal(("Kayıtlısınız", RowTone.Accent), (enrolled.Status, enrolled.StatusTone));
        Assert.Equal(("Dolu", RowTone.Warning), (full.Status, full.StatusTone));
        Assert.Equal("1 boş yer", teaching.Status);
        Assert.StartsWith("11/12 öğrenci · ", enrolled.Secondary, StringComparison.Ordinal);
        Assert.StartsWith("11/12 öğrenci", teaching.Secondary, StringComparison.Ordinal);
        Assert.Equal("Kapalı", new CourseItem(course with { Capacity = 0, StudentCount = 0 }, CourseItemStyle.Catalog).Row().Status);
        Assert.Equal("Sınırsız", new CourseItem(course with { Capacity = null }, CourseItemStyle.Catalog).Row().Status);
    }

    [Fact]
    public void LongAccountNames_KeepTheirState()
    {
        var row = new AccountItem(ListedAdministrationService.LongName).Row();

        Assert.Equal(("Devre dışı, şifresiz", RowTone.Warning), (row.Status, row.StatusTone));
        Assert.StartsWith("Öğrenci · ", row.Secondary, StringComparison.Ordinal);
    }

    /// <summary>The pill keeps its whole width at the right of a narrow line; the name gets what is left, never the pill's room.</summary>
    [Theory]
    [InlineData(400, 90)]
    [InlineData(120, 90)]
    [InlineData(60, 90)]
    public void StatusPill_NeverGivesWayToTheName(int lineWidth, int pillWidth)
    {
        var line = new Rectangle(10, 5, lineWidth, 20);

        var (primary, status) = ThemedListBox.SplitLine(line, pillWidth, 8);

        Assert.Equal(line.Right, status.Right);
        Assert.Equal(Math.Min(pillWidth, lineWidth), status.Width);
        Assert.False(primary.IntersectsWith(status));
        Assert.True(primary.Width >= 0);
        Assert.Equal(line, ThemedListBox.SplitLine(line, 0, 8).Primary);
    }

    /// <summary>A catalogue whose first course has a long name, a long teacher name and one free seat.</summary>
    private static ControlledCourseService LongCatalog()
    {
        var courses = new ControlledCourseService();
        courses.Courses.Insert(
            0,
            new CourseSummary(
                9,
                "Arayüz Tasarımının İlkeleri ve Kullanılabilirlik Testleri Üzerine Uygulamalı Çalışma Atölyesi ve Proje Değerlendirmesi",
                1,
                "Ayşegül Karamanlıoğlu Büyükçekmeceli Taşdelenoğulları",
                "Kullanıcı araştırması, prototip, test ve raporlama",
                12,
                11));
        return courses;
    }
}
