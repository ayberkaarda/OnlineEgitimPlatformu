using OnlineEgitim.Business.Services;
using OnlineEgitim.Domain.Entities;
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

    [Theory]
    [InlineData(SetupStage.EmptyDatabase, false, false)]
    [InlineData(SetupStage.FirstAdminNeedsKey, true, true)]
    [InlineData(SetupStage.StaffPasswordNeedsKey, true, true)]
    [InlineData(SetupStage.Completed, false, false)]
    public void SignIn_FounderLinks_FollowTheSetupStage(SetupStage stage, bool setupLink, bool keyRecovery) => Run(() =>
    {
        using var form = new GirisForm(new PendingAuthService(), new NoFormsFactory());
        Assert.False(form.OffersFirstAdminSetup);
        Assert.False(form.OffersSetupKeyRecovery);

        form.Stage = stage;

        Assert.Equal(setupLink, form.OffersFirstAdminSetup);
        Assert.Equal(keyRecovery, form.OffersSetupKeyRecovery);
        Assert.Contains("kurulum anahtarı", Find<LinkLabel>(form, "lnkKurulum").Text, StringComparison.Ordinal);
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

    /// <summary>
    /// The teacher closes the new course window (X or Alt+F4) while the course is being saved. The
    /// window stays open until the save finished, so the teacher panel reads the final number of new
    /// courses and reloads its list after the commit, once.
    /// </summary>
    [Fact]
    public void AddCourse_CloseWhilePending_RefreshesAfterCommit() => Run(() =>
    {
        var courses = new ControlledCourseService();
        using var form = new KursEklemeForm(courses, Sessions.Teacher) { Prompts = new RecordingPrompts() };
        _ = form.Handle; // a real window message loop, never shown
        Find<TextBox>(form, "txtKursAd").Text = "Grafik";
        var closedWith = new List<int>();
        form.FormClosed += (_, _) => closedWith.Add(form.CoursesAdded);

        var add = form.AddAsync();
        form.Close();

        Assert.False(form.IsDisposed);
        Assert.Empty(closedWith);
        courses.CompleteCreate();
        Application.DoEvents();

        Assert.True(add.Result);
        Assert.Equal([1], closedWith);
        Assert.True(form.IsDisposed);
        Assert.Equal(1, courses.CreateCalls);
    });

    [Fact]
    public void AddCourse_CloseWhileIdle_ClosesAtOnce() => Run(() =>
    {
        using var form = new KursEklemeForm(new ControlledCourseService(), Sessions.Teacher) { Prompts = new RecordingPrompts() };
        _ = form.Handle;

        form.Close();

        Assert.True(form.IsDisposed);
    });

    [Fact]
    public void Recovery_StudentAndTeacherModes_SendTheirProofOnce() => Run(() =>
    {
        var recovery = new PendingRecoveryService();
        var setup = new PendingSetupService();
        using var form = new HesapKurtarmaForm(recovery, setup) { Prompts = new RecordingPrompts() };
        Assert.False(form.TeacherMode);
        Assert.True(Find<RadioButton>(form, "rbOgretmen").Enabled); // staff accounts set a password with an administrator's code
        Assert.Equal(HesapKurtarmaForm.StudentHint, Find<Label>(form, "lblAciklama").Text);
        Find<TextBox>(form, "txtAd").Text = "Şifresiz Öğrenci";
        Find<TextBox>(form, "txtKod").Text = "ABCDE-FGHJK";

        var first = form.RecoverAsync();
        var second = form.RecoverAsync();

        Assert.Equal(["student:Şifresiz Öğrenci/ABCDE-FGHJK"], recovery.Calls);
        Assert.False(first.IsCompleted);
        Assert.Null(second.Result);
        Assert.False(Find<Button>(form, "btnKurtar").Enabled);
        Assert.False(Find<RadioButton>(form, "rbOgretmen").Enabled);
        recovery.RefuseRecovery();
        Assert.Null(first.Result);
        Assert.True(Find<Button>(form, "btnKurtar").Enabled);

        // Without the setup key the staff mode takes the administrator's code only, and fills in nothing.
        setup.EnvironmentKey = new SetupKeyPrefill("0123-4567-89AB-CDEF-0123-4567-89AB-CDEF", false);
        form.TeacherMode = true;
        Assert.Equal("Kurtarma Kodu", Find<Label>(form, "lblKod").Text);
        Assert.Equal(HesapKurtarmaForm.TeacherHint, Find<Label>(form, "lblAciklama").Text);
        Assert.Empty(Find<TextBox>(form, "txtKod").Text);
        form.TeacherMode = false;

        form.SetupKeyAccepted = true;
        Assert.Equal(HesapKurtarmaForm.StudentHintWithSetupKey, Find<Label>(form, "lblAciklama").Text);
        form.TeacherMode = true;
        Assert.Equal("Kod / Kurulum Anahtarı", Find<Label>(form, "lblKod").Text);
        Assert.Contains("docs/issue-setup-key.sql", Find<Label>(form, "lblAciklama").Text, StringComparison.Ordinal);
        Assert.Equal("0123-4567-89AB-CDEF-0123-4567-89AB-CDEF", Find<TextBox>(form, "txtKod").Text); // filled in, checked by the database
        Find<TextBox>(form, "txtKod").Text = "KURUCU-ANAHTARI";
        _ = form.RecoverAsync();
        Assert.Equal("teacher:Şifresiz Öğrenci/KURUCU-ANAHTARI", recovery.Calls[^1]);
        Assert.Empty(Find<TextBox>(form, "txtSifre").Text);
    });

    [Fact]
    public void RecoveryCode_WhilePending_IsNotIssuedTwiceAndShowsTheCode() => Run(() =>
    {
        var recovery = new PendingRecoveryService();
        using var form = new KurtarmaKoduForm(recovery, Sessions.Teacher);
        Find<TextBox>(form, "txtAd").Text = "Şifresiz Öğrenci";

        var first = form.IssueAsync();
        var second = form.IssueAsync();

        Assert.Equal(["issue:Şifresiz Öğrenci"], recovery.Calls);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.False(Find<Button>(form, "btnUret").Enabled);
        recovery.CompleteIssue("ABCDE-FGHJK");
        Assert.True(first.Result);
        Assert.Equal("ABCDE-FGHJK", Find<TextBox>(form, "txtKod").Text);
        Assert.True(Find<TextBox>(form, "txtKod").ReadOnly);
        Assert.StartsWith("Son geçerlilik: ", Find<Label>(form, "lblBitis").Text, StringComparison.Ordinal);
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
        Assert.Contains("docs/issue-setup-key.sql", Find<Label>(form, "lblAciklama").Text, StringComparison.Ordinal);
        Assert.False(first.IsCompleted);
        Assert.Null(second.Result);
        Assert.False(Find<Button>(form, "btnOlustur").Enabled);
        Assert.False(Find<Button>(form, "btnIptal").Enabled);
    });

    /// <summary>
    /// EGITIM_SETUP_KEY only fills in the key field. An invalid value is shown as a configuration error
    /// and fills in nothing; an empty database needs no key at all.
    /// </summary>
    [Fact]
    public void Setup_EnvironmentKey_OnlyFillsInTheForm() => Run(() =>
    {
        var setup = new PendingSetupService { EnvironmentKey = new SetupKeyPrefill("0123-4567-89AB-CDEF-0123-4567-89AB-CDEF", false) };
        using (var form = new IlkKurulumForm(setup) { Prompts = new RecordingPrompts() })
        {
            Assert.Equal("0123-4567-89AB-CDEF-0123-4567-89AB-CDEF", Find<TextBox>(form, "txtAnahtar").Text);
            Assert.Equal(0, setup.CreateCalls);
        }

        setup.EnvironmentKey = new SetupKeyPrefill(null, true);
        using (var form = new IlkKurulumForm(setup) { Prompts = new RecordingPrompts() })
        {
            Assert.Empty(Find<TextBox>(form, "txtAnahtar").Text);
            Assert.Contains(IlkKurulumForm.EnvironmentKeyInvalid, Find<Label>(form, "lblAciklama").Text, StringComparison.Ordinal);

            form.KeyRequired = false;
            Assert.Empty(Find<TextBox>(form, "txtAnahtar").Text);
            Assert.DoesNotContain("EGITIM_SETUP_KEY", Find<Label>(form, "lblAciklama").Text, StringComparison.Ordinal);
        }
    });
}
