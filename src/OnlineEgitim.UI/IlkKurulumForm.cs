using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// First-run setup: opens by itself on an empty database and from the sign-in while no teacher
    /// exists. The person installing the application proves it with the local setup key and creates
    /// the first teacher account with a password of their own; there is no default password.
    /// Closes with <see cref="DialogResult.OK"/> and <see cref="Session"/> set on success.
    /// </summary>
    public partial class IlkKurulumForm : Form
    {
        private readonly ISetupService _setup;
        private readonly ExclusiveOperation _create = new();

        public IlkKurulumForm(ISetupService setup)
        {
            ArgumentNullException.ThrowIfNull(setup);

            _setup = setup;
            Prompts = new MessageBoxPrompts(this);

            InitializeComponent();
            _create.HoldClosingOf(this);
            Icon = AppAssets.Icon;
            Text = AppText.Title + " - " + Text;
            lblAciklama.Text = SetupKeyHint(setup.IssueSetupKey());
            txtAnahtar.MaxLength = 64;
            txtAd.MaxLength = InputRules.PersonNameMaxLength;
            txtUzmanlik.MaxLength = InputRules.SpecialtyMaxLength;
            txtSifre.MaxLength = PasswordPolicy.MaxLength;
            txtSifreTekrar.MaxLength = PasswordPolicy.MaxLength;
            AcceptButton = btnOlustur;
            CancelButton = btnIptal;
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public UserSession? Session { get; private set; }

        /// <summary>Information dialogs; replaced in tests so no window opens.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal IUserPrompts Prompts { get; set; }

        /// <summary>Creates the first teacher; ignored while a previous attempt is still running.</summary>
        internal async Task<UserSession?> CreateAsync()
        {
            UserSession? session = null;
            await this.RunExclusiveAsync(
                _create,
                async () =>
                {
                    try
                    {
                        session = await _setup.CreateFirstTeacherAsync(txtAnahtar.Text, txtAd.Text, txtUzmanlik.Text, txtSifre.Text, txtSifreTekrar.Text);
                    }
                    finally
                    {
                        txtSifre.Clear();
                        txtSifreTekrar.Clear();
                        if (session is null)
                        {
                            _create.FocusAfterUnlock(txtSifre);
                        }
                        else
                        {
                            txtAnahtar.Clear();
                        }
                    }
                },
                btnOlustur,
                btnIptal);
            return session;
        }

        /// <summary>Where the installer finds the setup key; the key itself is never shown.</summary>
        internal static string SetupKeyHint(string? keyFilePath) =>
            "Henüz bir öğretmen hesabı yok. İlk öğretmen hesabını yalnızca bu bilgisayarın kurulum anahtarıyla oluşturabilirsiniz. "
            + (keyFilePath is null
                ? "Anahtar, EGITIM_SETUP_KEY ortam değişkeninde tanımlıdır."
                : "Anahtar şu dosyadadır:\n" + keyFilePath)
            + "\nAnahtar bir kez kullanılır; şifreyi kendiniz belirlersiniz.";

        private async void btnOlustur_Click(object? sender, EventArgs e)
        {
            var session = await CreateAsync();
            if (session is not null)
            {
                Prompts.Inform("Öğretmen hesabı oluşturuldu.");
                Session = session;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}
