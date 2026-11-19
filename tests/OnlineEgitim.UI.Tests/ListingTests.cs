using OnlineEgitim.Business.Services;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Tests.Support;
using static OnlineEgitim.UI.Tests.Support.StaThread;

namespace OnlineEgitim.UI.Tests;

public sealed class ListingTests
{
    [Fact]
    public void TeacherStudents_AnswersOutOfOrder_ShowOnlyTheLatestSelection() => Run(() =>
    {
        var courses = new ControlledCourseService();
        var enrollments = new ControlledEnrollmentService();
        using var form = new OgretmenForm(courses, enrollments, new NoFormsFactory(), Sessions.Teacher);
        Assert.True(form.LoadAsync().IsCompletedSuccessfully);
        var list = Find<ListBox>(form, "lstKurslar");
        var students = Find<ListBox>(form, "lstKayitliOgrenciler");

        list.SelectedIndex = 0; // request for course 1
        list.SelectedIndex = 1; // request for course 2
        Assert.Equal([1, 2], enrollments.StudentRequests.Select(r => r.CourseId));

        // The newer answer arrives first, then the stale one.
        enrollments.StudentRequests[1].Answer.SetResult(["Selin Deneme"]);
        enrollments.StudentRequests[0].Answer.SetResult(["Mert Deneme"]);

        Assert.Equal(["Selin Deneme"], students.Items.Cast<object>().Select(i => i.ToString()));
    });

    /// <summary>Refilling the course list is not a selection by the user: it requests no student list and clears the old one.</summary>
    [Fact]
    public void TeacherCourses_Reload_ProducesNoSelectionAndDropsPendingAnswers() => Run(() =>
    {
        var courses = new ControlledCourseService();
        var enrollments = new ControlledEnrollmentService();
        using var form = new OgretmenForm(courses, enrollments, new NoFormsFactory(), Sessions.Teacher);
        Assert.True(form.LoadAsync().IsCompletedSuccessfully);
        var list = Find<ListBox>(form, "lstKurslar");
        var students = Find<ListBox>(form, "lstKayitliOgrenciler");
        list.SelectedIndex = 0;
        enrollments.StudentRequests[0].Answer.SetResult(["Mert Deneme"]);
        list.SelectedIndex = 1; // still pending while the list is reloaded

        Assert.True(form.LoadAsync().IsCompletedSuccessfully);
        enrollments.StudentRequests[1].Answer.SetResult(["Selin Deneme"]);

        Assert.Equal(2, enrollments.StudentRequests.Count);
        Assert.Equal(-1, list.SelectedIndex);
        Assert.Empty(students.Items.Cast<object>());
        Assert.Equal(2, courses.TeachingCalls);
    });

    [Fact]
    public void Items_ShowTheOriginalFormatsWithoutParsingIds()
    {
        var course = new CourseSummary(12, "Temel Programlama", 1, "Deniz Örnek", "Döngüler");
        var noTopic = course with { Topic = null };

        Assert.Equal("12 - Temel Programlama / Deniz Örnek / Döngüler", new CourseItem(course, CourseItemStyle.Catalog).ToString());
        Assert.Equal("Temel Programlama / Deniz Örnek / -", new CourseItem(noTopic, CourseItemStyle.Mine).ToString());
        Assert.Equal("12 - Temel Programlama (0/sınırsız öğrenci)", new CourseItem(course, CourseItemStyle.Teaching).ToString());
        Assert.Equal("12 - Temel Programlama (3/20 öğrenci)", new CourseItem(course with { Capacity = 20, StudentCount = 3 }, CourseItemStyle.Teaching).ToString());
    }

    [Fact]
    public void Details_StudentSeesTheCountButNoNamesAndAHintInsteadOfNotes() => Run(() =>
    {
        var courses = new ControlledCourseService
        {
            Details = new CourseDetails(1, "Temel Programlama", "Döngüler", "Deniz Örnek", false, [], null, 3),
        };
        using var form = new KursDetayForm(courses, Sessions.Student, 1);

        var load = form.LoadAsync();

        Assert.True(load.IsCompletedSuccessfully && load.Result);
        Assert.Equal("Kayıtlı Öğrenci Sayısı: 3", Find<Label>(form, "label4").Text);
        Assert.False(Find<ListBox>(form, "lstOgrenciler").Visible);
        Assert.Empty(Find<ListBox>(form, "lstOgrenciler").Items.Cast<object>());
        Assert.Equal([KursDetayForm.NotesHidden], Find<ListBox>(form, "lstbNotlar").Items.Cast<object>().Select(i => i.ToString()));
    });

    [Fact]
    public void Details_TeacherSeesNamesAndNotes() => Run(() =>
    {
        var courses = new ControlledCourseService
        {
            Details = new CourseDetails(1, "Temel Programlama", null, "Deniz Örnek", true, ["Not"], ["Mert Deneme"], 1),
        };
        using var form = new KursDetayForm(courses, Sessions.Teacher, 1);

        Assert.True(form.LoadAsync().Result);
        Assert.Equal(["Mert Deneme"], Find<ListBox>(form, "lstOgrenciler").Items.Cast<object>().Select(i => i.ToString()));
        Assert.Equal(["Not"], Find<ListBox>(form, "lstbNotlar").Items.Cast<object>().Select(i => i.ToString()));
        Assert.Equal("-", Find<Label>(form, "lblUzmanlik").Text);
    });

    [Fact]
    public void Details_UnknownCourse_ReportsAndAsksToClose() => Run(() =>
    {
        using var form = new KursDetayForm(new ControlledCourseService(), Sessions.Teacher, 99);
        var before = NoDialogs.Requested.Count;

        var load = form.LoadAsync();

        Assert.True(load.IsCompletedSuccessfully);
        Assert.False(load.Result);
        Assert.Contains("Uyarı: Ders bulunamadı.", NoDialogs.Requested.Skip(before));
    });

    [Fact]
    public void Panels_ListTheCoursesOfTheirRole() => Run(() =>
    {
        var courses = new ControlledCourseService();
        var enrollments = new ControlledEnrollmentService();
        enrollments.Mine.Add(courses.Courses[1]);
        using var main = new MainForm(courses, enrollments, new NoFormsFactory(), Sessions.Student);
        using var mine = new KursNotlariForm(enrollments, Sessions.Student);

        Assert.True(main.LoadAsync().IsCompletedSuccessfully);
        Assert.True(mine.LoadAsync().IsCompletedSuccessfully);

        Assert.Equal(2, Find<ListBox>(main, "lstKurslar").Items.Count);
        Assert.Equal(["Veri Tabanına Giriş / Deniz Örnek / -"], Find<ListBox>(mine, "lstKurslar").Items.Cast<object>().Select(i => i.ToString()));
        Assert.Equal("Hoş geldin, Mert Deneme!", Find<Label>(main, "lblhosgeldin").Text);
    });
}
