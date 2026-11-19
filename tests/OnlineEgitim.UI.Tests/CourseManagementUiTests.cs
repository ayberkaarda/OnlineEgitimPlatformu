using OnlineEgitim.Business.Services;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Tests.Support;
using static OnlineEgitim.UI.Tests.Support.StaThread;

namespace OnlineEgitim.UI.Tests;

/// <summary>Editing and deleting a course from the teacher panel; no window is shown.</summary>
public sealed class CourseManagementUiTests
{
    [Fact]
    public void Delete_AsksWithTheStudentCountAndSendsItOnce() => Run(() =>
    {
        var courses = new ControlledCourseService
        {
            Details = new CourseDetails(1, "Temel Programlama", "Döngüler", "Deniz Örnek", true, [], ["Mert Deneme", "Selin Deneme"], 2),
        };
        var prompts = new RecordingPrompts();
        using var form = new OgretmenForm(courses, new ControlledEnrollmentService(), new NoFormsFactory(), Sessions.Teacher) { Prompts = prompts };
        Assert.True(form.LoadAsync().IsCompletedSuccessfully);
        Find<ListBox>(form, "lstKurslar").SelectedIndex = 0;

        var first = form.DeleteCourseAsync();
        var second = form.DeleteCourseAsync();

        Assert.Equal(["delete:1:2"], courses.Writes);
        Assert.Contains(prompts.Messages, m => m.Contains("2 kayıtlı öğrenci", StringComparison.Ordinal));
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.False(Find<Button>(form, "btnSil").Enabled);
        Assert.False(Find<Button>(form, "btnDuzenle").Enabled);

        var loads = courses.TeachingCalls;
        courses.CompleteWrite();
        Assert.True(first.Result);
        Assert.Equal(loads + 1, courses.TeachingCalls); // the list is reloaded after the deletion
        Assert.True(Find<Button>(form, "btnSil").Enabled);
        Assert.Equal("Kurs silindi.", prompts.Messages[^1]);
    });

    [Fact]
    public void Delete_Declined_SendsNothing() => Run(() =>
    {
        var courses = new ControlledCourseService
        {
            Details = new CourseDetails(1, "Temel Programlama", null, "Deniz Örnek", true, [], [], 0),
        };
        var prompts = new DecliningPrompts();
        using var form = new OgretmenForm(courses, new ControlledEnrollmentService(), new NoFormsFactory(), Sessions.Teacher) { Prompts = prompts };
        Assert.True(form.LoadAsync().IsCompletedSuccessfully);
        Find<ListBox>(form, "lstKurslar").SelectedIndex = 0;

        Assert.True(form.DeleteCourseAsync().Result);

        Assert.Empty(courses.Writes);
        Assert.Equal(["\"Temel Programlama\" kursu silinsin mi?"], prompts.Questions);
    });

    [Fact]
    public void Delete_WithoutASelection_AsksForOne() => Run(() =>
    {
        var courses = new ControlledCourseService();
        var prompts = new RecordingPrompts();
        using var form = new OgretmenForm(courses, new ControlledEnrollmentService(), new NoFormsFactory(), Sessions.Teacher) { Prompts = prompts };

        Assert.True(form.DeleteCourseAsync().Result);

        Assert.Empty(courses.Writes);
        Assert.Equal(["Lütfen bir kurs seçin."], prompts.Messages);
    });

    [Fact]
    public void Edit_SendsTheCapacityOnceAndReportsTheSave() => Run(() =>
    {
        var courses = new ControlledCourseService();
        using var form = new KursEklemeForm(courses, Sessions.Teacher) { Prompts = new RecordingPrompts() };
        form.BeginEdit(courses.Courses[0] with { Capacity = 12 });
        Assert.Equal("Temel Programlama", Find<TextBox>(form, "txtKursAd").Text);
        Assert.Equal(12, form.Capacity);
        Assert.False(Find<CheckBox>(form, "chkSinirsiz").Checked);
        Find<NumericUpDown>(form, "nudKontenjan").Value = 8;

        var first = form.AddAsync();
        var second = form.AddAsync();

        Assert.Equal(["update:1:Temel Programlama:8"], courses.Writes);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.Equal(0, courses.CreateCalls);
        courses.CompleteWrite();
        Assert.True(first.Result);
        Assert.True(form.Saved);
    });

    [Fact]
    public void Create_UnlimitedByDefaultAndNumberWhenUnticked() => Run(() =>
    {
        var courses = new ControlledCourseService();
        using var form = new KursEklemeForm(courses, Sessions.Teacher) { Prompts = new RecordingPrompts() };
        Assert.True(Find<CheckBox>(form, "chkSinirsiz").Checked);
        Assert.False(Find<NumericUpDown>(form, "nudKontenjan").Enabled);
        Find<TextBox>(form, "txtKursAd").Text = "Grafik";

        _ = form.AddAsync();
        Assert.Null(courses.LastCapacity);
        courses.CompleteCreate();

        Find<CheckBox>(form, "chkSinirsiz").Checked = false;
        Assert.True(Find<NumericUpDown>(form, "nudKontenjan").Enabled);
        Find<NumericUpDown>(form, "nudKontenjan").Value = 0;
        _ = form.AddAsync();
        Assert.Equal(0, courses.LastCapacity);
    });

    /// <summary>Answers every confirmation with no.</summary>
    private sealed class DecliningPrompts : IUserPrompts
    {
        public List<string> Questions { get; } = [];

        public bool Confirm(string question)
        {
            Questions.Add(question);
            return false;
        }

        public void Inform(string message)
        {
        }

        public void Warn(string message)
        {
        }
    }
}
