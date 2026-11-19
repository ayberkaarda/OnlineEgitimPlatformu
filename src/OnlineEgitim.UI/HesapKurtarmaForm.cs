using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// First password for an account the original application left without one. A student proves the
    /// right with the single use code a teacher issued; while no teacher can sign in with a password,
    /// the founder uses the setup key the database owner created.
    /// Closes with <see cref="DialogResult.OK"/> and <see cref="Session"/> set on success.
    /// </summary>
    public partial class HesapKurtarmaForm : Form
    {
        internal const string StudentHint =
            "Eski sistemden şifresiz kalan öğrenci hesapları için dersinizin öğretmeninden tek kullanımlık bir kurtarma kodu isteyin. "
            + "Kodla yeni şifrenizi belirlediğinizde hesabınız ve ders kayıtlarınız aynen korunur.";

        internal const string StudentHintWithSetupKey =
            StudentHint + " Yalnızca hiçbir derse kayıtlı değilseniz kurucu, kod yerine kurulum anahtarıyla şifrenizi belirleyebilir; "
            + "bir derse kayıtlıysanız kodu o dersin öğretmeni üretir.";

        internal const string TeacherHint =
            "Şifreyle giriş yapabilen bir öğretmen yokken, şifresi olmayan bir öğretmen hesabına kurulum anahtarıyla şifre belirlenir. "
            + IlkKurulumForm.SetupKeySource;

        private bool _setupKeyAccepted;

        private readonly IRecoveryService _recovery;
        private readonly ISetupService _setup;
        private readonly ExclusiveOperation _recover = new();

        public HesapKurtarmaForm(IRecoveryService recovery, ISetupService setup)
        {
            ArgumentNullException.ThrowIfNull(recovery);
            ArgumentNullException.ThrowIfNull(setup);

            _recovery = recovery;
            _setup = setup;
            Prompts = new MessageBoxPrompts(this);

            InitializeComponent();
            _recover.HoldClosingOf(this);
            Icon = AppAssets.Icon;
            Text = AppText.Title + " - " + Text;
            txtAd.MaxLength = InputRules.PersonNameMaxLength;
            txtKod.MaxLength = 64;
            txtSifre.MaxLength = PasswordPolicy.MaxLength;
            txtSifreTekrar.MaxLength = PasswordPolicy.MaxLength;
            AcceptButton = btnKurtar;
            CancelButton = btnIptal;
            SetupKeyAccepted = false;
        }

        /// <summary>
        /// Whether the founder's recovery with the setup key is offered: only while no teacher can sign
        /// in with a password. The database decides again when the key is used.
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool SetupKeyAccepted
        {
            get => _setupKeyAccepted;
            set
            {
                _setupKeyAccepted = value;
                rbOgretmen.Enabled = value;
                if (!value)
                {
                    TeacherMode = false;
                }

                ShowMode();
            }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public UserSession? Session { get; private set; }

        /// <summary>Information dialogs; replaced in tests so no window opens.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal IUserPrompts Prompts { get; set; }

        /// <summary>Whether the teacher recovery (with the setup key) is selected.</summary>
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

        private void ShowMode()
        {
            txtKod.Clear();
            if (TeacherMode)
            {
                var prefill = _setup.GetEnvironmentKey();
                lblKod.Text = "Kurulum Anahtarı";
                lblAciklama.Text = TeacherHint + (prefill.Invalid ? "\n" + IlkKurulumForm.EnvironmentKeyInvalid : string.Empty);
                txtKod.Text = prefill.Key ?? string.Empty;
            }
            else
            {
                lblKod.Text = "Kurtarma Kodu";
                lblAciklama.Text = _setupKeyAccepted ? StudentHintWithSetupKey : StudentHint;
            }
        }

        private void rbRol_CheckedChanged(object? sender, EventArgs e)
        {
            if (sender is RadioButton { Checked: true })
            {
                ShowMode();
            }
        }

        private async void btnKurtar_Click(object? sender, EventArgs e)
        {
            var session = await RecoverAsync();
            if (session is not null)
            {
                Prompts.Inform("Şifreniz belirlendi. Bundan sonra bu şifreyle giriş yapabilirsiniz.");
                Session = session;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}
