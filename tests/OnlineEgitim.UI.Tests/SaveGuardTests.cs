using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Tests.Support;
using static OnlineEgitim.UI.Tests.Support.StaThread;

namespace OnlineEgitim.UI.Tests;

/// <summary>Every write button is exclusive: a second click while the first call is pending is ignored.</summary>
public sealed class SaveGuardTests
{
    [Fact]
    public void StudentSignIn_WhilePending_IsNotSentTwice() => Run(() =>
    {
        var auth = new PendingAuthService();
        using var form = new GirisForm(auth, new NoFormsFactory()) { Prompts = new RecordingPrompts() };
        Find<TextBox>(form, "txtAd").Text = "Mert Deneme";
        Find<TextBox>(form, "txtSifre").Text = "Gizli.Sifre1";

        var first = form.SignInAsync();
        var second = form.SignInAsync();

        Assert.Equal(1, auth.StudentSignInCalls);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompletedSuccessfully);
        Assert.Null(second.Result);
        Assert.False(Find<Button>(form, "btnOgrenciGiris").Enabled);
        Assert.False(Find<Button>(form, "btnOgretmenGiris").Enabled);
        Assert.False(Find<Button>(form, "button1").Enabled);
        Assert.False(Find<LinkLabel>(form, "lnkKurulum").Enabled);
    });

    [Fact]
    public void SignIn_TeacherSetupLink_OnlyWhileNoTeacherExists() => Run(() =>
    {
        using var form = new GirisForm(new PendingAuthService(), new NoFormsFactory());
        Assert.False(form.OfferTeacherSetup);

        form.OfferTeacherSetup = true;

        Assert.True(form.OfferTeacherSetup);
        Assert.Contains("kurucu anahtarı", Find<LinkLabel>(form, "lnkKurulum").Text, StringComparison.Ordinal);
    });

    [Fact]
    public void TeacherSignIn_WhilePending_IsNotSentTwice() => Run(() =>
    {
        var auth = new PendingAuthService();
        using var form = new OgretmenGirisForm(auth) { Prompts = new RecordingPrompts() };
        Find<TextBox>(form, "txtAd").Text = "Deniz Örnek";
        Find<TextBox>(form, "txtSifre").Text = "Gizli.Sifre1";

        var first = form.SignInAsync();
        var second = form.SignInAsync();

        Assert.Equal(1, auth.TeacherSignInCalls);
        Assert.Equal(0, auth.StudentSignInCalls);
        Assert.False(first.IsCompleted);
        Assert.Null(second.Result);
        Assert.False(Find<Button>(form, "btnGiris").Enabled);
    });

    [Fact]
    public void Register_WhilePending_IsNotSentTwice() => Run(() =>
    {
        var auth = new PendingAuthService();
        using var form = new KayitForm(auth) { Prompts = new RecordingPrompts() };
        Find<TextBox>(form, "txtAd").Text = "Ayşe Yılmaz";

        var first = form.SaveAsync();
        var second = form.SaveAsync();

        Assert.Equal(1, auth.RegisterCalls);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.False(Find<Button>(form, "btnKaydet").Enabled);
    });

    [Fact]
    public void Enroll_WhilePending_IsNotSentTwice() => Run(() =>
    {
        var enrollments = new ControlledEnrollmentService();
        var courses = new ControlledCourseService();
        using var form = new MainForm(courses, enrollments, new NoFormsFactory(), Sessions.Student) { Prompts = new RecordingPrompts() };
        var list = Find<ListBox>(form, "lstKurslar");
        list.Items.Add(new CourseItem(courses.Courses[0], CourseItemStyle.Catalog));
        list.SelectedIndex = 0;

        var first = form.EnrollAsync();
        var second = form.EnrollAsync();

        Assert.Equal(1, enrollments.EnrollCalls);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.False(Find<Button>(form, "btnDersSec").Enabled);
    });

    [Fact]
    public void Enroll_WithoutASelection_AsksForOneAndCallsNothing() => Run(() =>
    {
        var enrollments = new ControlledEnrollmentService();
        var prompts = new RecordingPrompts();
        using var form = new MainForm(new ControlledCourseService(), enrollments, new NoFormsFactory(), Sessions.Student) { Prompts = prompts };

        var run = form.EnrollAsync();

        Assert.True(run.IsCompletedSuccessfully);
        Assert.Equal(0, enrollments.EnrollCalls);
        Assert.Equal(["Lütfen bir ders seçiniz."], prompts.Messages);
    });

    [Fact]
    public void AddNote_WhilePending_IsNotSentTwice() => Run(() =>
    {
        var courses = new ControlledCourseService();
        using var form = new OgretmenForm(courses, new ControlledEnrollmentService(), new NoFormsFactory(), Sessions.Teacher) { Prompts = new RecordingPrompts() };
        Find<TextBox>(form, "txtNotEkle").Text = "Not";

        var first = form.AddNoteAsync();
        var second = form.AddNoteAsync();

        Assert.Equal(1, courses.NoteCalls);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.False(Find<Button>(form, "btnNotEkle").Enabled);
    });

    [Fact]
    public void AddCourse_WhilePending_IsNotSentTwice() => Run(() =>
    {
        var courses = new ControlledCourseService();
        using var form = new KursEklemeForm(courses, Sessions.Teacher) { Prompts = new RecordingPrompts() };
        Find<TextBox>(form, "txtKursAd").Text = "Grafik";

        var first = form.AddAsync();
        var second = form.AddAsync();

        Assert.Equal(1, courses.CreateCalls);
        Assert.False(first.IsCompleted);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.False(Find<Button>(form, "btnEkle").Enabled);

        courses.CompleteCreate();
        Assert.Equal(1, form.CoursesAdded);
    });

    [Fact]
    public void Setup_WhilePending_IsNotSentTwice() => Run(() =>
    {
        var setup = new PendingSetupService();
        using var form = new IlkKurulumForm(setup) { Prompts = new RecordingPrompts() };
        Find<TextBox>(form, "txtAnahtar").Text = "ABCDE-FGHJK-MNPQR-STVWX";

        var first = form.CreateAsync();
        var second = form.CreateAsync();

        Assert.Equal(1, setup.CreateCalls);
        Assert.Equal("ABCDE-FGHJK-MNPQR-STVWX", setup.LastKey);
        Assert.True(Find<TextBox>(form, "txtAnahtar").UseSystemPasswordChar);
        Assert.Contains(@"C:\Kurulum\setup-key.txt", Find<Label>(form, "lblAciklama").Text, StringComparison.Ordinal);
        Assert.False(first.IsCompleted);
        Assert.Null(second.Result);
        Assert.False(Find<Button>(form, "btnOlustur").Enabled);
        Assert.False(Find<Button>(form, "btnIptal").Enabled);
    });
}
