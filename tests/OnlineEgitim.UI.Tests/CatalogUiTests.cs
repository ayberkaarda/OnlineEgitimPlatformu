using OnlineEgitim.Business.Services;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Tests.Support;
using static OnlineEgitim.UI.Tests.Support.StaThread;

namespace OnlineEgitim.UI.Tests;

/// <summary>The student's catalogue: search requests, pages and leaving a course; no window is shown.</summary>
public sealed class CatalogUiTests
{
    [Fact]
    public void Construction_SearchesNothingUntilLoaded() => Run(() =>
    {
        var courses = new ControlledCourseService();
        using var form = new MainForm(courses, new ControlledEnrollmentService(), new NoFormsFactory(), Sessions.Student);

        Assert.Empty(courses.Searches);
        Assert.Equal(0, Find<ComboBox>(form, "cmbKayit").SelectedIndex);
    });

    [Fact]
    public void Search_AnswersOutOfOrder_ShowOnlyTheLatest() => Run(() =>
    {
        var courses = new ControlledCourseService { HoldSearches = true };
        using var form = new MainForm(courses, new ControlledEnrollmentService(), new NoFormsFactory(), Sessions.Student);
        var list = Find<ListBox>(form, "lstKurslar");

        var older = form.SearchAsync(0);
        var newer = form.SearchAsync(1);
        courses.Searches[1].Answer.SetResult(ControlledCourseService.Page([courses.Courses[1]], pageIndex: 1, total: 21));
        courses.Searches[0].Answer.SetResult(ControlledCourseService.Page([courses.Courses[0]], pageIndex: 0, total: 21));

        Assert.True(older.IsCompletedSuccessfully && newer.IsCompletedSuccessfully);
        Assert.Equal([2], list.Items.Cast<CourseItem>().Select(i => i.Course.Id));
        Assert.Equal(-1, list.SelectedIndex); // refilling is not a selection
        Assert.Equal("Sayfa 2 / 2 (21 kurs)", Find<Label>(form, "lblSayfa").Text);
        Assert.True(Find<Button>(form, "btnOnceki").Enabled);
        Assert.False(Find<Button>(form, "btnSonraki").Enabled);
    });

    [Fact]
    public void Search_TheFiltersMakeTheRequest() => Run(() =>
    {
        var courses = new ControlledCourseService();
        using var form = new MainForm(courses, new ControlledEnrollmentService(), new NoFormsFactory(), Sessions.Student);
        Find<TextBox>(form, "txtAra").Text = "prog";
        Find<TextBox>(form, "txtKonu").Text = "döngü";
        Find<TextBox>(form, "txtOgretmen").Text = "Deniz";

        Find<CheckBox>(form, "chkBosKontenjan").Checked = true; // a change by the user searches from page 1
        Find<ComboBox>(form, "cmbKayit").SelectedIndex = 2;

        Assert.Equal(2, courses.Searches.Count);
        Assert.Equal(new CourseSearch("prog", "döngü", "Deniz", true, EnrollmentFilter.NotEnrolled, 0), courses.Searches[^1].Search);
    });

    [Fact]
    public void Leave_AsksOnceSendsOnceAndRefreshes() => Run(() =>
    {
        var courses = new ControlledCourseService();
        var enrollments = new ControlledEnrollmentService();
        var prompts = new RecordingPrompts();
        using var form = new MainForm(courses, enrollments, new NoFormsFactory(), Sessions.Student) { Prompts = prompts };
        Assert.True(form.LoadAsync().IsCompletedSuccessfully);
        Find<ListBox>(form, "lstKurslar").SelectedIndex = 1;

        var first = form.LeaveAsync();
        var second = form.LeaveAsync();

        Assert.Equal([2], enrollments.LeaveCalls);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.False(Find<Button>(form, "btnAyril").Enabled);
        Assert.False(Find<Button>(form, "btnDersSec").Enabled);
        Assert.Contains(prompts.Messages, m => m.StartsWith("\"Veri Tabanına Giriş\" dersinden ayrılmak", StringComparison.Ordinal));

        var searches = courses.Searches.Count;
        enrollments.Leave.SetResult();
        Assert.True(first.Result);
        Assert.Equal(searches + 1, courses.Searches.Count);
        Assert.Equal("Dersten ayrıldınız.", prompts.Messages[^1]);
        Assert.True(Find<Button>(form, "btnAyril").Enabled);
    });

    [Fact]
    public void CatalogItems_ShowSeatsAndOwnEnrollment()
    {
        var course = new CourseSummary(12, "Temel Programlama", 1, "Deniz Örnek", "Döngüler", 20, 3);

        Assert.Equal("12 - Temel Programlama / Deniz Örnek / Döngüler / 3/20 öğrenci / Kayıtlısınız", new CourseItem(course, CourseItemStyle.Catalog, true).ToString());
        Assert.Equal("12 - Temel Programlama / Deniz Örnek / - / 0/sınırsız öğrenci", new CourseItem(course with { Topic = null, Capacity = null, StudentCount = 0 }, CourseItemStyle.Catalog).ToString());
    }
}
