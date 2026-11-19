using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// First password for an account the original application left without one. A student proves the
    /// right with the single use code a teacher issued, a teacher with the setup key of this computer.
    /// Closes with <see cref="DialogResult.OK"/> and <see cref="Session"/> set on success.
    /// </summary>
    public partial class HesapKurtarmaForm : Form
    {
        internal const string StudentHint =
            "Eski sistemden şifresiz kalan öğrenci hesapları için öğretmeninizden tek kullanımlık bir kurtarma kodu isteyin. "
            + "Kodla yeni şifrenizi belirlediğinizde hesabınız ve ders kayıtlarınız aynen korunur.";

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
            Icon = AppAssets.Icon;
            Text = AppText.Title + " - " + Text;
            txtAd.MaxLength = InputRules.PersonNameMaxLength;
            txtKod.MaxLength = 64;
            txtSifre.MaxLength = PasswordPolicy.MaxLength;
            txtSifreTekrar.MaxLength = PasswordPolicy.MaxLength;
            AcceptButton = btnKurtar;
            CancelButton = btnIptal;
            ShowMode();
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
            if (TeacherMode)
            {
                lblKod.Text = "Kurulum Anahtarı";
                lblAciklama.Text = "Şifresi olmayan bir öğretmen hesabına bu bilgisayarın kurulum anahtarıyla şifre belirlenir. "
                    + (_setup.IssueSetupKey() is { } path ? "Anahtar şu dosyadadır:\n" + path : "Anahtar, EGITIM_SETUP_KEY ortam değişkeninde tanımlıdır.")
                    + "\nAnahtar bir kez kullanılır.";
            }
            else
            {
                lblKod.Text = "Kurtarma Kodu";
                lblAciklama.Text = StudentHint;
            }

            txtKod.Clear();
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
