using System.Globalization;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Theming;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// Administrator panel: searching accounts, creating teacher and student accounts with a single use
    /// code (shown once, never logged), disabling and enabling accounts and making an administrator a
    /// teacher. Every rule is decided by the service; this window only shows its answers.
    /// </summary>
    public sealed class YoneticiForm : AppDialog, ISessionPanel
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
        private string? _signOutNotice;

        private readonly Label lblBaslik = new() { Name = nameof(lblBaslik), AutoSize = true, UseMnemonic = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly TextBox txtAra = new() { Name = nameof(txtAra), MaxLength = InputRules.EmailMaxLength, PlaceholderText = "Ad, e-posta ya da uzmanlık" };
        private readonly ThemedComboBox cmbRol = FormLayout.Picker(nameof(cmbRol), 150);
        private readonly ThemedButton btnAra = FormLayout.Button(nameof(btnAra), "Ara", ButtonKind.Secondary, Glyphs.Search);
        private readonly ThemedListBox lstHesaplar = new() { Name = nameof(lstHesaplar), AccessibleName = "Hesaplar" };
        private readonly ThemedButton btnOnceki = FormLayout.Button(nameof(btnOnceki), "‹ Önceki", ButtonKind.Subtle);
        private readonly Label lblSayfa = FormLayout.Hint(string.Empty);
        private readonly ThemedButton btnSonraki = FormLayout.Button(nameof(btnSonraki), "Sonraki ›", ButtonKind.Subtle);
        private readonly ThemedButton btnSifirlamaKodu = FormLayout.Button(nameof(btnSifirlamaKodu), "Sıfırlama kodu üret", ButtonKind.Secondary, Glyphs.Key);
        private readonly ThemedButton btnEtkinlestir = FormLayout.Button(nameof(btnEtkinlestir), "Etkinleştir", ButtonKind.Secondary);
        private readonly ThemedButton btnYoneticilikten = FormLayout.Button(nameof(btnYoneticilikten), "Yöneticilikten çıkar", ButtonKind.Secondary);
        private readonly ThemedButton btnDevreDisi = FormLayout.Button(nameof(btnDevreDisi), "Devre dışı bırak", ButtonKind.Danger);
        private readonly SegmentButton rbOgretmen = new() { Name = nameof(rbOgretmen), Text = "Öğretmen", Checked = true };
        private readonly SegmentButton rbOgrenci = new() { Name = nameof(rbOgrenci), Text = "Öğrenci" };
        private readonly TextBox txtAd = new() { Name = nameof(txtAd), MaxLength = InputRules.PersonNameMaxLength };
        private readonly TextBox txtDetay = new() { Name = nameof(txtDetay) };
        private readonly ThemedButton btnOlustur = FormLayout.Button(nameof(btnOlustur), "Hesabı oluştur", ButtonKind.Primary, Glyphs.AddPerson);
        private readonly TextBox txtKod = new() { Name = nameof(txtKod), PlaceholderText = "Kod burada bir kez gösterilir" };
        private readonly Label lblKodBitis = FormLayout.Hint(string.Empty);
        private readonly ThemedButton btnSifremiDegistir = FormLayout.Button(nameof(btnSifremiDegistir), "Şifremi değiştir", ButtonKind.Secondary, Glyphs.Lock);
        private readonly ThemedButton btnCikis = FormLayout.Button(nameof(btnCikis), "Oturumu kapat", ButtonKind.Secondary);
        private Label lblDetay = null!;

        public YoneticiForm(IAdministrationService administration, IFormFactory forms, UserSession session)
            : base(
                "Yönetici paneli",
                "Hesapları arayın; hesap açın, devre dışı bırakın ya da sıfırlama kodu üretin.",
                Glyphs.Admin,
                new Size(1120, 660),
                new Size(960, 580))
        {
            ArgumentNullException.ThrowIfNull(administration);
            ArgumentNullException.ThrowIfNull(forms);
            ArgumentNullException.ThrowIfNull(session);

            _administration = administration;
            _forms = forms;
            _session = session;
            BuildLayout();

            // A close request (Çıkış, X, Alt+F4) waits for a running operation, so a code that is being
            // issued is still shown and the result of a change is known before the window goes.
            _create.HoldClosingOf(this);
            _change.HoldClosingOf(this);
            lblBaslik.Text = session.DisplayName + Environment.NewLine + "Yönetici hesabı";
            cmbRol.Items.AddRange(["Tümü", "Öğrenci", "Öğretmen", "Yönetici"]);
            cmbRol.SelectedIndex = 0; // before _ready: not a choice of the user, no search
            ShowAccountKind();
            _ready = true;

            Load += async (_, _) => await LoadAsync();
            cmbRol.SelectedIndexChanged += async (_, _) =>
            {
                if (_ready)
                {
                    await SearchAsync(0);
                }
            };
            rbOgrenci.CheckedChanged += rbTur_CheckedChanged;
            rbOgretmen.CheckedChanged += rbTur_CheckedChanged;
            btnAra.Click += async (_, _) => await SearchAsync(0);
            btnOnceki.Click += async (_, _) => await SearchAsync(Math.Max(0, _pageIndex - 1));
            btnSonraki.Click += async (_, _) => await SearchAsync(_pageIndex + 1);
            btnOlustur.Click += async (_, _) => await CreateAccountAsync();
            btnDevreDisi.Click += async (_, _) => await SetDisabledAsync(true);
            btnEtkinlestir.Click += async (_, _) => await SetDisabledAsync(false);
            btnYoneticilikten.Click += async (_, _) => await DemoteAsync();
            btnSifirlamaKodu.Click += async (_, _) => await IssueResetCodeAsync();
            btnSifremiDegistir.Click += (_, _) => ChangePassword();
            btnCikis.Click += (_, _) => EndSession();
        }

        bool ISessionPanel.SignedOut => _signedOut;

        string? ISessionPanel.SignOutNotice => _signOutNotice;

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
            lstHesaplar.IsLoading = true;
            try
            {
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
            finally
            {
                if (_searchRequests.IsLatest(request))
                {
                    lstHesaplar.IsLoading = false;
                }
            }
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

        /// <summary>Added to the code's note when a close request was put off so that the code could be read.</summary>
        internal const string CloseKeptForCode = "Pencere, kodu görebilmeniz için açık kaldı; kodu ilettikten sonra kapatın.";

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
                _signOutNotice = done + " Bu değişiklik kendi oturumunuzu da kapattı.";
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

        private void ClearCode()
        {
            txtKod.Clear();
            lblKodBitis.Text = string.Empty;
        }

        private void ShowAccountKind()
        {
            lblDetay.Text = rbOgrenci.Checked ? "E-posta" : "Uzmanlık alanı";
            txtDetay.AccessibleName = lblDetay.Text;
            txtDetay.MaxLength = rbOgrenci.Checked ? InputRules.EmailMaxLength : InputRules.SpecialtyMaxLength;
        }

        private void rbTur_CheckedChanged(object? sender, EventArgs e)
        {
            if (sender is RadioButton { Checked: true })
            {
                ShowAccountKind();
            }
        }

        private void ChangePassword()
        {
            if (PasswordChange.Show(this, _forms, _session))
            {
                _signOutNotice = SifreDegistirForm.Done;
                EndSession();
            }
        }

        private void BuildLayout()
        {
            Name = nameof(YoneticiForm);
            lblBaslik.Font = Theme.Fonts.Body;
            lblBaslik.ForeColor = Theme.Current.TextSecondary;
            SetHeaderAside(lblBaslik);

            // Accounts: the search, the list with its pages and the actions on the selected account.
            btnAra.Margin = Padding.Empty;
            var search = FormLayout.FieldRow(
                "searchRow",
                (FormLayout.Field("&Ara", FormLayout.Input(txtAra)), 100),
                (FormLayout.Field("&Rol", cmbRol), 0),
                (FormLayout.FieldAction(btnAra), 0));
            lstHesaplar.Describe = item => item is AccountItem account ? account.Row() : new ListRow(item.ToString() ?? string.Empty);
            lstHesaplar.EmptyText = "Aramanıza uyan hesap yok.";
            lblSayfa.Name = nameof(lblSayfa);
            lblSayfa.TextAlign = ContentAlignment.MiddleCenter;
            lblSayfa.Margin = new Padding(Spacing.S, 0, Spacing.S, 0);
            btnOnceki.Enabled = false;
            btnSonraki.Enabled = false;
            btnOnceki.Margin = btnSonraki.Margin = Padding.Empty;
            var pager = FormLayout.Bar("pager", (btnOnceki, false), (FormLayout.Spacer("pagerSpacerLeft"), true), (lblSayfa, false), (FormLayout.Spacer("pagerSpacerRight"), true), (btnSonraki, false));
            pager.Margin = new Padding(0, Spacing.S, 0, 0);
            btnEtkinlestir.Margin = btnYoneticilikten.Margin = new Padding(0, 0, Spacing.S, 0);
            btnDevreDisi.Margin = Padding.Empty;
            var actions = FormLayout.Bar(
                "accountActions",
                (btnEtkinlestir, false),
                (btnYoneticilikten, false),
                (FormLayout.Spacer(), true),
                (btnDevreDisi, false));
            actions.Margin = new Padding(0, Spacing.M, 0, 0);
            var accountsCard = FormLayout.Card("accountsCard", "Hesaplar", FormLayout.Page("accountsPage", (search, false), (lstHesaplar, true), (pager, false), (actions, false)));
            accountsCard.Margin = new Padding(0, 0, Spacing.S, 0);

            // A new account: its kind, name and detail.
            var kind = new FlowLayoutPanel
            {
                Name = "kindSegments",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = Padding.Empty,
            };
            kind.Controls.Add(rbOgretmen);
            kind.Controls.Add(rbOgrenci);
            var kindField = FormLayout.Field("Hesap türü", kind);
            var name = FormLayout.Field("Ad &soyad", FormLayout.Input(txtAd));
            var detail = FormLayout.Field("Uzmanlık alanı", FormLayout.Input(txtDetay));
            lblDetay = (Label)detail.Controls[0];
            lblDetay.Name = nameof(lblDetay);
            lblDetay.UseMnemonic = false;
            var person = FormLayout.FieldRow("personRow", (name, 50), (detail, 50));
            kindField.Margin = new Padding(0, 0, 0, Spacing.M);
            btnOlustur.AutoSize = false;
            btnOlustur.Dock = DockStyle.Top;
            btnOlustur.Margin = new Padding(0, Spacing.XS, 0, 0);
            var newForm = FormLayout.Stack("newAccountForm", kindField, person, btnOlustur);
            btnOlustur.Dock = DockStyle.Top;
            var newCard = FormLayout.Card("newAccountCard", "Yeni hesap", newForm, fill: false);

            // The single use code of the last new account or of a reset of the selected account, shown once.
            btnSifirlamaKodu.Margin = new Padding(0, 0, 0, Spacing.M);
            btnSifirlamaKodu.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            var codeField = FormLayout.Field("Tek kullanımlık kod", FormLayout.CodeInput(txtKod));
            codeField.Margin = new Padding(0, 0, 0, Spacing.XS);
            lblKodBitis.Name = nameof(lblKodBitis);
            lblKodBitis.Margin = Padding.Empty;
            var codeCard = FormLayout.Card("codeCard", "Seçili hesap", FormLayout.Stack("codeStack", btnSifirlamaKodu, codeField, lblKodBitis), fill: false);
            codeCard.Edge = Theme.Current.Warm;
            codeCard.Margin = Padding.Empty;

            var right = FormLayout.Page("rightColumn", (newCard, false), (codeCard, false));
            Content.Controls.Add(FormLayout.Columns("layout", accountsCard, 62, right));

            AcceptButton = btnAra;
            CancelButton = btnCikis;
            SetFooterNote("Kodu kişiye güvenli bir yolla iletin; kod yalnızca bu pencerede, bir kez gösterilir.");
            SetFooterButtons(btnSifremiDegistir, btnCikis);
            FinishLayout();
        }
    }
}
