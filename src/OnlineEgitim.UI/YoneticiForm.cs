using System.Globalization;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// Administrator panel: searching accounts, creating teacher and student accounts with a single use
    /// code (shown once, never logged), disabling and enabling accounts and making an administrator a
    /// teacher. Every rule is decided by the service; this window only shows its answers.
    /// </summary>
    public partial class YoneticiForm : Form, ISessionPanel
    {
        private readonly IAdministrationService _administration;
        private readonly IFormFactory _forms;
        private readonly UserSession _session;
        private readonly ExclusiveOperation _create = new();
        private readonly ExclusiveOperation _change = new();
        private readonly RequestSequence _searchRequests = new();
        private readonly bool _ready;
        private int _pageIndex;
        private bool _signedOut;

        public YoneticiForm(IAdministrationService administration, IFormFactory forms, UserSession session)
        {
            ArgumentNullException.ThrowIfNull(administration);
            ArgumentNullException.ThrowIfNull(forms);
            ArgumentNullException.ThrowIfNull(session);

            _administration = administration;
            _forms = forms;
            _session = session;
            Prompts = new MessageBoxPrompts(this);

            InitializeComponent();

            // A close request (Çıkış, X, Alt+F4) waits for a running operation, so a code that is being
            // issued is still shown and the result of a change is known before the window goes.
            _create.HoldClosingOf(this);
            _change.HoldClosingOf(this);
            Icon = AppAssets.Icon;
            Text = AppText.Title + " - " + Text;
            lblBaslik.Text = "Kullanıcı Yönetimi - " + session.DisplayName;
            txtAra.MaxLength = InputRules.EmailMaxLength;
            txtAd.MaxLength = InputRules.PersonNameMaxLength;
            cmbRol.Items.AddRange(["Tümü", "Öğrenci", "Öğretmen", "Yönetici"]);
            cmbRol.SelectedIndex = 0; // before _ready: not a choice of the user, no search
            ShowAccountKind();
            AcceptButton = btnAra;
            _ready = true;
            Load += async (_, _) => await LoadAsync();
        }

        /// <summary>Information dialogs; replaced in tests so no window opens.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal IUserPrompts Prompts { get; set; }

        bool ISessionPanel.SignedOut => _signedOut;

        /// <summary>Whether the session ended and the sign-in has to be shown again.</summary>
        internal bool SignedOut => _signedOut;

        void ISessionPanel.EndSession() => EndSession();

        /// <summary>The account selected in the list, or <see langword="null"/>.</summary>
        internal AccountListing? Selected => (lstHesaplar.SelectedItem as AccountItem)?.Account;

        internal Task LoadAsync() => SearchAsync(0);

        /// <summary>Searches the accounts; only the answer to the latest search is shown and refilling selects nothing.</summary>
        internal async Task SearchAsync(int pageIndex)
        {
            var request = _searchRequests.Next();
            AccountRole? role = cmbRol.SelectedIndex switch
            {
                1 => AccountRole.Student,
                2 => AccountRole.Teacher,
                3 => AccountRole.Admin,
                _ => null,
            };
            var search = new AccountSearch(txtAra.Text, role, pageIndex);
            await this.RunAsync(async () =>
            {
                var result = await _administration.SearchAccountsAsync(_session, search);
                if (!_searchRequests.IsLatest(request) || IsDisposed)
                {
                    return;
                }

                _pageIndex = result.PageIndex;
                lstHesaplar.ReplaceItems(result.Items.Select(a => new AccountItem(a)));
                lstHesaplar.ClearSelected();
                lblSayfa.Text = string.Create(
                    CultureInfo.CurrentCulture,
                    $"Sayfa {result.PageIndex + 1} / {result.PageCount} ({result.TotalCount} hesap)");
                btnOnceki.Enabled = result.PageIndex > 0;
                btnSonraki.Enabled = result.PageIndex + 1 < result.PageCount;
            });
        }

        /// <summary>
        /// Creates the account and shows its single use code once; ignored while a previous one is
        /// still being created.
        /// </summary>
        internal async Task<bool> CreateAccountAsync()
        {
            var created = false;
            var ran = await this.RunExclusiveAsync(
                _create,
                async () =>
                {
                    ClearCode();
                    var code = rbOgrenci.Checked
                        ? await _administration.CreateStudentAsync(_session, txtAd.Text, txtDetay.Text)
                        : await _administration.CreateTeacherAsync(_session, txtAd.Text, txtDetay.Text);
                    ShowCode(code, "Hesap oluşturuldu. Kişi bu kodla ilk şifresini \"Kodla şifre belirle\" ekranından belirler.");
                    txtAd.Clear();
                    txtDetay.Clear();
                    created = true;
                    _create.FocusAfterUnlock(txtKod);
                },
                btnOlustur,
                btnSifirlamaKodu,
                rbOgretmen,
                rbOgrenci);
            if (created)
            {
                await SearchAsync(_pageIndex);
            }

            return ran;
        }

        /// <summary>
        /// Issues a single use reset code for the selected account and shows it once; ignored while a
        /// previous one is still being issued. The account's password works until the code is used.
        /// </summary>
        internal Task<bool> IssueResetCodeAsync() =>
            this.RunExclusiveAsync(
                _create,
                async () =>
                {
                    ClearCode();
                    if (Selected is not { } account)
                    {
                        Prompts.Warn("Lütfen bir hesap seçin.");
                        return;
                    }

                    var code = await _administration.IssueResetCodeAsync(_session, account.Role, account.Id);
                    ShowCode(code, $"\"{account.FullName}\" bu kodla \"Kodla şifre belirle\" ekranından yeni şifresini belirler; açık oturumları o anda kapanır.");
                    _create.FocusAfterUnlock(txtKod);
                },
                btnSifirlamaKodu,
                btnOlustur);

        /// <summary>Disables or enables the selected account after a confirmation.</summary>
        internal Task<bool> SetDisabledAsync(bool disabled) =>
            ChangeSelectedAsync(
                account => disabled
                    ? $"\"{account.FullName}\" hesabı devre dışı bırakılsın mı? Hesap giriş yapamaz ve açık oturumları kapanır."
                    : $"\"{account.FullName}\" hesabı yeniden etkinleştirilsin mi?",
                account => _administration.SetDisabledAsync(_session, account.Role, account.Id, disabled),
                disabled ? "Hesap devre dışı bırakıldı." : "Hesap etkinleştirildi.",
                endsOwnSession: disabled);

        /// <summary>Makes the selected administrator a teacher after a confirmation.</summary>
        internal Task<bool> DemoteAsync() =>
            ChangeSelectedAsync(
                account => $"\"{account.FullName}\" yöneticilikten çıkarılıp öğretmen yapılsın mı? Açık oturumları kapanır.",
                account => account.Role == AccountRole.Admin
                    ? _administration.DemoteAdminAsync(_session, account.Id)
                    : throw new Business.BusinessRuleException("Lütfen bir yönetici hesabı seçin."),
                "Hesap artık öğretmen.",
                endsOwnSession: true);

        private async Task<bool> ChangeSelectedAsync(
            Func<AccountListing, string> question,
            Func<AccountListing, Task> change,
            string done,
            bool endsOwnSession)
        {
            var changed = false;
            var own = false;
            var ran = await this.RunExclusiveAsync(
                _change,
                async () =>
                {
                    if (Selected is not { } account)
                    {
                        Prompts.Warn("Lütfen bir hesap seçin.");
                        return;
                    }

                    if (!Prompts.Confirm(question(account)))
                    {
                        return;
                    }

                    await change(account);
                    changed = true;
                    own = endsOwnSession && account.Role == _session.Role && account.Id == _session.UserId;
                    Prompts.Inform(done);
                },
                btnDevreDisi,
                btnEtkinlestir,
                btnYoneticilikten);
            if (own)
            {
                // The change ended this session too.
                EndSession();
            }
            else if (changed)
            {
                await SearchAsync(_pageIndex);
            }

            return ran;
        }

        private void EndSession()
        {
            _signedOut = true;
            SessionEnd.Close(this);
        }

        /// <summary>
        /// Shows the single use code. The code exists only here, so a close request that arrived while it was
        /// issued is dropped: the window stays open until the administrator closes it after reading the code.
        /// </summary>
        private void ShowCode(RecoveryCode code, string note)
        {
            txtKod.Text = code.Code;
            lblKodBitis.Text = note + " Son geçerlilik: " + code.ExpiresUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
            if (_create.DropPendingClose())
            {
                lblKodBitis.Text += " " + CloseKeptForCode;
            }
        }

        /// <summary>Added to the code's note when a close request was put off so that the code could be read.</summary>
        internal const string CloseKeptForCode = "Pencere, kodu görebilmeniz için açık kaldı; kodu ilettikten sonra kapatın.";

        private void ClearCode()
        {
            txtKod.Clear();
            lblKodBitis.Text = string.Empty;
        }

        private void ShowAccountKind()
        {
            lblDetay.Text = rbOgrenci.Checked ? "E-posta" : "Uzmanlık Alanı";
            txtDetay.MaxLength = rbOgrenci.Checked ? InputRules.EmailMaxLength : InputRules.SpecialtyMaxLength;
        }

        private void rbTur_CheckedChanged(object? sender, EventArgs e)
        {
            if (sender is RadioButton { Checked: true })
            {
                ShowAccountKind();
            }
        }

        private async void btnAra_Click(object? sender, EventArgs e) => await SearchAsync(0);

        private async void cmbRol_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_ready)
            {
                await SearchAsync(0);
            }
        }

        private async void btnOnceki_Click(object? sender, EventArgs e) => await SearchAsync(Math.Max(0, _pageIndex - 1));

        private async void btnSonraki_Click(object? sender, EventArgs e) => await SearchAsync(_pageIndex + 1);

        private async void btnOlustur_Click(object? sender, EventArgs e) => await CreateAccountAsync();

        private async void btnDevreDisi_Click(object? sender, EventArgs e) => await SetDisabledAsync(true);

        private async void btnEtkinlestir_Click(object? sender, EventArgs e) => await SetDisabledAsync(false);

        private async void btnYoneticilikten_Click(object? sender, EventArgs e) => await DemoteAsync();

        private void btnCikis_Click(object? sender, EventArgs e) => EndSession();

        private async void btnSifirlamaKodu_Click(object? sender, EventArgs e) => await IssueResetCodeAsync();

        private void btnSifremiDegistir_Click(object? sender, EventArgs e)
        {
            if (PasswordChange.Show(this, _forms, _session))
            {
                EndSession();
            }
        }
    }
}
