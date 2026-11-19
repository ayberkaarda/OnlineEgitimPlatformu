using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Tests.Support;
using static OnlineEgitim.UI.Tests.Support.StaThread;

namespace OnlineEgitim.UI.Tests;

/// <summary>
/// The focus is moved only after the guarded controls are enabled again; a disabled control cannot
/// take it. Forms get window handles but are never shown.
/// </summary>
public sealed class FocusTests
{
    [Fact]
    public void FocusAfterUnlock_SelectsTheTargetOnlyOnceItIsEnabledAgain() => Run(() =>
    {
        using var form = new Form();
        using var button = new Button();
        using var field = new TextBox();
        form.Controls.AddRange([button, field]);
        CreateHandles(form);
        var operation = new ExclusiveOperation();
        var pending = new TaskCompletionSource();

        var run = operation.RunAsync(
            async () =>
            {
                await pending.Task;
                Assert.False(field.Enabled);
                operation.FocusAfterUnlock(field);
                Assert.NotSame(field, form.ActiveControl);
            },
            button,
            field);
        pending.SetResult();

        Assert.True(run.IsCompletedSuccessfully);
        Assert.True(field.Enabled);
        Assert.Same(field, form.ActiveControl);
    });

    [Fact]
    public void RefusedStudentSignIn_ClearsAndSelectsThePasswordAfterUnlock() => Run(() =>
    {
        var auth = new PendingAuthService();
        using var form = new GirisForm(auth, new NoFormsFactory()) { Prompts = new RecordingPrompts() };
        CreateHandles(form);
        var password = Find<TextBox>(form, "txtSifre");
        Find<TextBox>(form, "txtAd").Text = "Mert Deneme";
        password.Text = "yanlis";
        Find<Button>(form, "button1").Select();

        var signIn = form.SignInAsync();
        auth.RefuseSignIn();

        Assert.True(signIn.IsCompletedSuccessfully);
        Assert.Null(signIn.Result);
        Assert.True(Find<Button>(form, "btnOgrenciGiris").Enabled);
        Assert.Empty(password.Text);
        Assert.Same(password, form.ActiveControl);
        Assert.Equal("yanlis", auth.LastPassword);
    });

    [Fact]
    public void Registration_ClearsTheFormAndSelectsTheNameAfterUnlock() => Run(() =>
    {
        var auth = new PendingAuthService();
        var prompts = new RecordingPrompts();
        using var form = new KayitForm(auth) { Prompts = prompts };
        CreateHandles(form);
        var name = Find<TextBox>(form, "txtAd");
        name.Text = "Ayşe Yılmaz";
        Find<TextBox>(form, "txtMail").Text = "ayse@example.com";
        Find<TextBox>(form, "txtSifre").Text = "Gizli.Sifre1";
        Find<Button>(form, "btnKaydet").Select();

        var save = form.SaveAsync();
        auth.CompleteRegister();

        Assert.True(save.IsCompletedSuccessfully && save.Result);
        Assert.Empty(name.Text);
        Assert.Empty(Find<TextBox>(form, "txtSifre").Text);
        Assert.Same(name, form.ActiveControl);
        Assert.Equal(["Kayıt başarıyla tamamlandı!"], prompts.Messages);
    });

    [Fact]
    public void AddedNote_ClearsAndSelectsTheNoteAfterUnlock() => Run(() =>
    {
        var courses = new ControlledCourseService();
        using var form = new OgretmenForm(courses, new ControlledEnrollmentService(), new NoFormsFactory(), Sessions.Teacher) { Prompts = new RecordingPrompts() };
        CreateHandles(form);
        var note = Find<TextBox>(form, "txtNotEkle");
        note.Text = "Yeni not";
        Find<Button>(form, "btnYeniKurs").Select();

        var save = form.AddNoteAsync();
        courses.CompleteNote();

        Assert.True(save.IsCompletedSuccessfully && save.Result);
        Assert.Empty(note.Text);
        Assert.Same(note, form.ActiveControl);
    });

    [Fact]
    public void RefusedSetup_ClearsBothPasswordsAndSelectsTheFirst() => Run(() =>
    {
        var setup = new PendingSetupService();
        using var form = new IlkKurulumForm(setup) { Prompts = new RecordingPrompts() };
        CreateHandles(form);
        var password = Find<TextBox>(form, "txtSifre");
        password.Text = "Gizli.Sifre1";
        Find<TextBox>(form, "txtSifreTekrar").Text = "Gizli.Sifre2";

        var create = form.CreateAsync();
        setup.Refuse();

        Assert.Null(create.Result);
        Assert.Empty(password.Text);
        Assert.Empty(Find<TextBox>(form, "txtSifreTekrar").Text);
        Assert.Same(password, form.ActiveControl);
    });

    private static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls)
        {
            CreateHandles(child);
        }
    }
}
