using System.Globalization;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Theming;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// A password for an account without one: left so by the original application or created by an
    /// administrator. The person proves the right with the single use code a teacher (for their
    /// students) or an administrator issued; while no teacher or administrator can sign in with a
    /// password, the founder may use the setup key the database owner created instead.
    /// Closes with <see cref="DialogResult.OK"/> and <see cref="Session"/> set on success.
    /// </summary>
    public sealed class HesapKurtarmaForm : AppDialog
    {
        internal const string StudentHint =
            "Şifreniz yoksa veya unuttuysanız dersinizin öğretmeninden ya da yöneticiden tek kullanımlık bir kurtarma kodu isteyin; kod 24 saat geçerlidir. "
            + "Kodla yeni şifrenizi belirlediğinizde hesabınız ve ders kayıtlarınız aynen korunur.";

        internal const string StudentHintWithSetupKey =
            StudentHint + " Yalnızca hiçbir derse kayıtlı değilseniz kurucu, kod yerine kurulum anahtarıyla şifrenizi belirleyebilir; "
            + "bir derse kayıtlıysanız kodu o dersin öğretmeni üretir.";

        internal const string TeacherHint =
            "Şifresi olmayan veya şifresini unutan öğretmen ve yönetici hesapları için yöneticinin verdiği tek kullanımlık kodu girin; kod 24 saat geçerlidir.";

        internal const string TeacherHintWithSetupKey =
            TeacherHint + " Şifreyle giriş yapabilen bir öğretmen veya yönetici yokken şifresiz bir hesaba kurulum anahtarıyla da şifre belirlenebilir. "
            + IlkKurulumForm.SetupKeySource;

        private bool _setupKeyAccepted;

        private readonly IRecoveryService _recovery;
        private readonly ISetupService _setup;
        private readonly ExclusiveOperation _recover = new();

        private readonly SegmentButton rbOgrenci = new() { Name = nameof(rbOgrenci), Text = "Öğrenci", Checked = true };
        private readonly SegmentButton rbOgretmen = new() { Name = nameof(rbOgretmen), Text = "Öğretmen / Yönetici" };
        private readonly Label lblAciklama = FormLayout.Text(nameof(lblAciklama));
        private readonly TextBox txtAd = new() { Name = nameof(txtAd), MaxLength = InputRules.PersonNameMaxLength };
        private readonly TextBox txtKod = new() { Name = nameof(txtKod), MaxLength = 64, UseSystemPasswordChar = true };
        private readonly TextBox txtSifre = new() { Name = nameof(txtSifre), MaxLength = PasswordPolicy.MaxLength, UseSystemPasswordChar = true };
        private readonly TextBox txtSifreTekrar = new() { Name = nameof(txtSifreTekrar), MaxLength = PasswordPolicy.MaxLength, UseSystemPasswordChar = true };
        private readonly ThemedButton btnKurtar = FormLayout.Button(nameof(btnKurtar), "Şifreyi belirle", ButtonKind.Primary);
        private readonly ThemedButton btnIptal = FormLayout.Button(nameof(btnIptal), "İptal", ButtonKind.Secondary);
        private Label lblKod = null!;

        public HesapKurtarmaForm(IRecoveryService recovery, ISetupService setup)
            : base("Kodla şifre belirle", "Tek kullanımlık kodla ilk şifrenizi ya da unuttuğunuz şifrenin yerine yenisini belirleyin.", Glyphs.Key, new Size(640, 600), new Size(520, 520), modal: true)
        {
            ArgumentNullException.ThrowIfNull(recovery);
            ArgumentNullException.ThrowIfNull(setup);

            _recovery = recovery;
            _setup = setup;
            BuildLayout();
            _recover.HoldClosingOf(this);
            SetupKeyAccepted = false;

            rbOgrenci.CheckedChanged += rbRol_CheckedChanged;
            rbOgretmen.CheckedChanged += rbRol_CheckedChanged;
            btnKurtar.Click += async (_, _) => await RecoverAndCloseAsync();
        }

        /// <summary>
        /// Whether the founder's recovery with the setup key is offered: only while no teacher or
        /// administrator can sign in with a password. The database decides again when the key is used.
        /// The code is accepted in both modes at any time.
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool SetupKeyAccepted
        {
            get => _setupKeyAccepted;
            set
            {
                _setupKeyAccepted = value;
                ShowMode();
            }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public UserSession? Session { get; private set; }

        /// <summary>Whether the staff recovery (teacher or administrator) is selected.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool TeacherMode
        {
            get => rbOgretmen.Checked;
            set
            {
                rbOgretmen.Checked = value;
                rbOgrenci.Checked = !value;
            }
        }

        /// <summary>Sets the first password; ignored while a previous attempt is still running.</summary>
        internal async Task<UserSession?> RecoverAsync()
        {
            UserSession? session = null;
            await this.RunExclusiveAsync(
                _recover,
                async () =>
                {
                    try
                    {
                        session = TeacherMode
                            ? await _recovery.RecoverTeacherAsync(txtKod.Text, txtAd.Text, txtSifre.Text, txtSifreTekrar.Text)
                            : await _recovery.RecoverStudentAsync(txtAd.Text, txtKod.Text, txtSifre.Text, txtSifreTekrar.Text);
                    }
                    finally
                    {
                        txtSifre.Clear();
                        txtSifreTekrar.Clear();
                        if (session is null)
                        {
                            _recover.FocusAfterUnlock(txtKod);
                        }
                        else
                        {
                            txtKod.Clear();
                        }
                    }
                },
                btnKurtar,
                btnIptal,
                rbOgrenci,
                rbOgretmen);
            return session;
        }

        private async Task RecoverAndCloseAsync()
        {
            var session = await RecoverAsync();
            if (session is not null)
            {
                Session = session;
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void ShowMode()
        {
            txtKod.Clear();
            if (TeacherMode && _setupKeyAccepted)
            {
                var prefill = _setup.GetEnvironmentKey();
                lblKod.Text = "Kod / Kurulum Anahtarı";
                lblAciklama.Text = TeacherHintWithSetupKey + (prefill.Invalid ? "\n" + IlkKurulumForm.EnvironmentKeyInvalid : string.Empty);
                txtKod.Text = prefill.Key ?? string.Empty;
            }
            else if (TeacherMode)
            {
                lblKod.Text = "Kurtarma Kodu";
                lblAciklama.Text = TeacherHint;
            }
            else
            {
                lblKod.Text = "Kurtarma Kodu";
                lblAciklama.Text = _setupKeyAccepted ? StudentHintWithSetupKey : StudentHint;
            }

            txtKod.AccessibleName = lblKod.Text;
        }

        private void rbRol_CheckedChanged(object? sender, EventArgs e)
        {
            if (sender is RadioButton { Checked: true })
            {
                ShowMode();
            }
        }

        private void BuildLayout()
        {
            Name = nameof(HesapKurtarmaForm);
            var role = new FlowLayoutPanel
            {
                Name = "roleSegments",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, Spacing.L),
            };
            role.Controls.Add(rbOgrenci);
            role.Controls.Add(rbOgretmen);
            var roleField = FormLayout.Field("Hesap türü", role);
            roleField.Margin = new Padding(0, 0, 0, Spacing.M);

            var note = FormLayout.Note("recoveryNote", Glyphs.Info, lblAciklama);
            var name = FormLayout.Field("&Ad soyad", FormLayout.Input(txtAd));
            var code = FormLayout.Field("Kurtarma Kodu", FormLayout.Input(txtKod));
            lblKod = (Label)code.Controls[0];
            lblKod.Name = nameof(lblKod);
            lblKod.UseMnemonic = false;
            var passwords = FormLayout.FieldRow(
                "passwordRow",
                (FormLayout.Field("&Yeni şifre", FormLayout.Input(txtSifre), string.Create(CultureInfo.CurrentCulture, $"En az {PasswordPolicy.MinLength} karakter.")), 50),
                (FormLayout.Field("Yeni şifre &tekrar", FormLayout.Input(txtSifreTekrar)), 50));
            name.Margin = code.Margin = new Padding(0, 0, 0, Spacing.M);

            var card = FormLayout.Card("recoveryCard", null, FormLayout.Stack("recoveryForm", roleField, name, code, passwords), fill: false);
            card.Margin = Padding.Empty;
            role.Dock = DockStyle.None;
            role.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            Content.Controls.Add(FormLayout.Page("recoveryPage", (note, false), (card, false)));

            AcceptButton = btnKurtar;
            CancelButton = btnIptal;
            btnIptal.DialogResult = DialogResult.Cancel;
            SetFooterNote("Şifre belirlenince hesabınızla giriş yapılır.");
            SetFooterButtons(btnKurtar, btnIptal);
            FinishLayout();
        }
    }
}
