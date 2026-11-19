using OnlineEgitim.Business;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Tests.Support;
using static OnlineEgitim.UI.Tests.Support.StaThread;

namespace OnlineEgitim.UI.Tests;

/// <summary>The administrator panel and the end of a refused session; no window is shown.</summary>
public sealed class AdministrationUiTests
{
    private static readonly UserSession Admin = new(AccountRole.Admin, 1, "Ayla Yönetici");

    [Fact]
    public void CreateAccount_SentOnceAndTheCodeShownOnce() => Run(() =>
    {
        var service = new ControlledAdministrationService();
        using var form = new YoneticiForm(service, new NoFormsFactory(), Admin) { Prompts = new RecordingPrompts() };
        Find<RadioButton>(form, "rbOgrenci").Checked = true;
        Assert.Equal("E-posta", Find<Label>(form, "lblDetay").Text);
        Find<TextBox>(form, "txtAd").Text = "Yeni Öğrenci";
        Find<TextBox>(form, "txtDetay").Text = "yeni@example.com";

        var first = form.CreateAccountAsync();
        var second = form.CreateAccountAsync();

        Assert.Equal(["student:Yeni Öğrenci/yeni@example.com"], service.Calls);
        Assert.True(second.IsCompletedSuccessfully && !second.Result);
        Assert.False(Find<Button>(form, "btnOlustur").Enabled);
        service.Create.SetResult(new RecoveryCode("ABCDE-FGHJK", new DateTime(2026, 3, 3, 10, 0, 0, DateTimeKind.Utc)));

        Assert.True(first.Result);
        Assert.Equal("ABCDE-FGHJK", Find<TextBox>(form, "txtKod").Text);
        Assert.True(Find<TextBox>(form, "txtKod").ReadOnly);
        Assert.Empty(Find<TextBox>(form, "txtAd").Text);

        // The next creation clears the shown code before it is sent.
        Find<TextBox>(form, "txtAd").Text = "Başka Öğrenci";
        _ = form.CreateAccountAsync();
        Assert.Empty(Find<TextBox>(form, "txtKod").Text);
    });

    /// <summary>
    /// "Çıkış" (or X) right after "Oluştur" while the account is still being created: the window waits
    /// for the answer, shows the single use code and stays open so it can be read; the session still
    /// ends, and the next close goes through.
    /// </summary>
    [Fact]
    public void CreateAccount_ClosingWhileRunning_WaitsAndShowsTheCode() => Run(() =>
    {
        var service = new ControlledAdministrationService();
        using var form = new YoneticiForm(service, new NoFormsFactory(), Admin) { Prompts = new RecordingPrompts() };
        _ = form.Handle; // a real window message loop, never shown
        Find<TextBox>(form, "txtAd").Text = "Yeni Öğretmen";
        Find<TextBox>(form, "txtDetay").Text = "Matematik";

        var create = form.CreateAccountAsync();
        ((ISessionPanel)form).EndSession(); // what "Çıkış" does
        Application.DoEvents(); // the posted close arrives while the account is being created
        form.Close();

        Assert.False(form.IsDisposed);
        service.Create.SetResult(new RecoveryCode("ABCDE-FGHJK", new DateTime(2026, 3, 3, 10, 0, 0, DateTimeKind.Utc)));
        Application.DoEvents();

        Assert.True(create.Result);
        Assert.False(form.IsDisposed);
        Assert.Equal("ABCDE-FGHJK", Find<TextBox>(form, "txtKod").Text);
        Assert.EndsWith(YoneticiForm.CloseKeptForCode, Find<Label>(form, "lblKodBitis").Text, StringComparison.Ordinal);
        Assert.True(form.SignedOut);

        form.Close();
        Assert.True(form.IsDisposed);
    });

    [Fact]
    public void ResetCode_ClosingWhileRunning_WaitsAndShowsTheCode() => Run(() =>
    {
        var service = new ControlledAdministrationService();
        service.NextPage = Page(new AccountListing(AccountRole.Student, 2, "Mert Deneme", "mert@example.com", false, true));
        using var form = new YoneticiForm(service, new NoFormsFactory(), Admin) { Prompts = new RecordingPrompts() };
        _ = form.Handle;
        Assert.True(form.LoadAsync().IsCompletedSuccessfully);
        Find<ListBox>(form, "lstHesaplar").SelectedIndex = 0;

        var issue = form.IssueResetCodeAsync();
        form.Close();
        Assert.False(form.IsDisposed);
        service.Create.SetResult(new RecoveryCode("KLMNP-QRSTV", new DateTime(2026, 3, 3, 10, 0, 0, DateTimeKind.Utc)));
        Application.DoEvents();

        Assert.True(issue.Result);
        Assert.False(form.IsDisposed);
        Assert.Equal("KLMNP-QRSTV", Find<TextBox>(form, "txtKod").Text);
    });

    [Fact]
    public void Search_AnswersOutOfOrder_ShowOnlyTheLatest() => Run(() =>
    {
        var service = new ControlledAdministrationService { HoldSearches = true };
        using var form = new YoneticiForm(service, new NoFormsFactory(), Admin);
        Assert.Empty(service.Searches);

        var older = form.SearchAsync(0);
        Find<ComboBox>(form, "cmbRol").SelectedIndex = 3;
        service.Searches[1].Answer.SetResult(Page(new AccountListing(AccountRole.Admin, 1, "Ayla Yönetici", "Yönetim", false, true)));
        service.Searches[0].Answer.SetResult(Page(new AccountListing(AccountRole.Student, 2, "Mert Deneme", "mert@example.com", true, false)));

        Assert.True(older.IsCompletedSuccessfully);
        Assert.Equal(AccountRole.Admin, service.Searches[1].Search.Role);
        var list = Find<ListBox>(form, "lstHesaplar");
        Assert.Equal(["[Yönetici] Ayla Yönetici - Yönetim"], list.Items.Cast<object>().Select(i => i.ToString()));
        Assert.Equal(-1, list.SelectedIndex);
        Assert.Equal("[Öğrenci] Mert Deneme - mert@example.com (devre dışı, şifresiz)", new AccountItem(new AccountListing(AccountRole.Student, 2, "Mert Deneme", "mert@example.com", true, false)).ToString());
    });

    /// <summary>Disabling one's own account (allowed only with another administrator) ends the session at once.</summary>
    [Fact]
    public void DisableOwnAccount_EndsTheSession() => Run(() =>
    {
        var service = new ControlledAdministrationService();
        service.NextPage = Page(new AccountListing(AccountRole.Admin, 1, "Ayla Yönetici", "Yönetim", false, true));
        using var form = new YoneticiForm(service, new NoFormsFactory(), Admin) { Prompts = new RecordingPrompts() };
        Assert.True(form.LoadAsync().IsCompletedSuccessfully);
        Find<ListBox>(form, "lstHesaplar").SelectedIndex = 0;

        var run = form.SetDisabledAsync(true);
        var twice = form.SetDisabledAsync(true);
        service.Change.SetResult();

        Assert.True(run.Result);
        Assert.False(twice.Result);
        Assert.Equal(["disable:Admin/1"], service.Calls);
        Assert.True(form.SignedOut);
    });

    /// <summary>A refused session in any window ends the panel's session, so the sign-in comes back.</summary>
    [Fact]
    public void ExpiredSession_EndsThePanelSession() => Run(() =>
    {
        var service = new ControlledAdministrationService();
        using var form = new YoneticiForm(service, new NoFormsFactory(), Admin) { Prompts = new RecordingPrompts() };
        service.ExpireSearches = true;

        Assert.True(form.SearchAsync(0).IsCompletedSuccessfully);

        Assert.True(form.SignedOut);
        Assert.Contains("Uyarı: " + "Oturumunuz sona erdi. Lütfen yeniden giriş yapın.", NoDialogs.Requested);
    });

    private static AccountSearchResult Page(params AccountListing[] items) => new(items, items.Length, 0, AccountSearch.DefaultPageSize);

    private sealed class ControlledAdministrationService : IAdministrationService
    {
        public bool HoldSearches { get; set; }

        public bool ExpireSearches { get; set; }

        public AccountSearchResult NextPage { get; set; } = Page();

        public List<string> Calls { get; } = [];

        public List<(AccountSearch Search, TaskCompletionSource<AccountSearchResult> Answer)> Searches { get; } = [];

        public TaskCompletionSource<RecoveryCode> Create { get; private set; } = new();

        public TaskCompletionSource Change { get; private set; } = new();

        public Task<AccountSearchResult> SearchAccountsAsync(UserSession admin, AccountSearch search, CancellationToken cancellationToken = default)
        {
            if (ExpireSearches)
            {
                return Task.FromException<AccountSearchResult>(new SessionExpiredException());
            }

            var answer = new TaskCompletionSource<AccountSearchResult>();
            Searches.Add((search, answer));
            if (!HoldSearches)
            {
                answer.SetResult(NextPage);
            }

            return answer.Task;
        }

        public Task<RecoveryCode> CreateTeacherAsync(UserSession admin, string? fullName, string? specialty, CancellationToken cancellationToken = default)
        {
            Calls.Add("teacher:" + fullName + "/" + specialty);
            Create = new TaskCompletionSource<RecoveryCode>();
            return Create.Task;
        }

        public Task<RecoveryCode> CreateStudentAsync(UserSession admin, string? fullName, string? email, CancellationToken cancellationToken = default)
        {
            Calls.Add("student:" + fullName + "/" + email);
            Create = new TaskCompletionSource<RecoveryCode>();
            return Create.Task;
        }

        public Task SetDisabledAsync(UserSession admin, AccountRole role, int accountId, bool disabled, CancellationToken cancellationToken = default)
        {
            Calls.Add((disabled ? "disable:" : "enable:") + role + "/" + accountId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Change = new TaskCompletionSource();
            return Change.Task;
        }

        public Task<RecoveryCode> IssueResetCodeAsync(UserSession admin, AccountRole role, int accountId, CancellationToken cancellationToken = default)
        {
            Calls.Add("reset:" + role + "/" + accountId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Create = new TaskCompletionSource<RecoveryCode>();
            return Create.Task;
        }

        public Task DemoteAdminAsync(UserSession admin, int accountId, CancellationToken cancellationToken = default)
        {
            Calls.Add("demote:" + accountId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Change = new TaskCompletionSource();
            return Change.Task;
        }
    }
}
