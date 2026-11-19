using OnlineEgitim.Business;
using OnlineEgitim.UI.Tests.Support;
using static OnlineEgitim.UI.Tests.Support.StaThread;

namespace OnlineEgitim.UI.Tests;

/// <summary>The own password change; no window is shown.</summary>
public sealed class PasswordChangeUiTests
{
    [Fact]
    public void Change_SentOnceAndThePasswordsAreClearedEitherWay() => Run(() =>
    {
        var auth = new PendingAuthService();
        var prompts = new RecordingPrompts();
        using var form = new SifreDegistirForm(auth, Sessions.Student) { Prompts = prompts };
        Assert.True(Find<TextBox>(form, "txtMevcut").UseSystemPasswordChar);
        Find<TextBox>(form, "txtMevcut").Text = "Eski.Sifre1";
        Find<TextBox>(form, "txtYeni").Text = "Yeni.Sifre1";
        Find<TextBox>(form, "txtYeniTekrar").Text = "Yeni.Sifre1";

        var first = form.ChangeAsync();
        var second = form.ChangeAsync();

        Assert.Equal(1, auth.ChangeCalls);
        Assert.Equal("Eski.Sifre1", auth.LastPassword);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.False(Find<Button>(form, "btnDegistir").Enabled);
        Assert.False(Find<Button>(form, "btnIptal").Enabled);

        auth.Change.SetException(new BusinessRuleException("Şifre değiştirilemedi."));
        Assert.True(first.Result);
        Assert.False(form.Changed);
        Assert.Empty(Find<TextBox>(form, "txtMevcut").Text);
        Assert.Empty(Find<TextBox>(form, "txtYeni").Text);

        Find<TextBox>(form, "txtMevcut").Text = "Eski.Sifre1";
        var retry = form.ChangeAsync();
        auth.Change.SetResult();
        Assert.True(retry.Result);
        Assert.True(form.Changed);
        Assert.Equal(SifreDegistirForm.Done, prompts.Messages[^1]);
        Assert.Empty(Find<TextBox>(form, "txtYeniTekrar").Text);
    });
}
