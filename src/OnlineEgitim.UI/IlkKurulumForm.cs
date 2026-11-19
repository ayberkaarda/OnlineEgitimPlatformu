using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// First-run setup: opens by itself on an empty database (no key needed) and from the sign-in while
    /// students exist but no teacher, where the setup key created in the database is required. The
    /// person installing the application creates the first teacher account with a password of their
    /// own; there is no default password. Closes with <see cref="DialogResult.OK"/> and
    /// <see cref="Session"/> set on success.
    /// </summary>
    public partial class IlkKurulumForm : Form
    {
        private readonly ISetupService _setup;
        private readonly ExclusiveOperation _create = new();
        private bool _keyRequired;

        public IlkKurulumForm(ISetupService setup)
        {
            ArgumentNullException.ThrowIfNull(setup);

            _setup = setup;
            Prompts = new MessageBoxPrompts(this);

            InitializeComponent();
            _create.HoldClosingOf(this);
            Icon = AppAssets.Icon;
            Text = AppText.Title + " - " + Text;
            KeyRequired = true;
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

        /// <summary>
        /// Whether the setup key field is shown: true from the sign-in (the database has students),
        /// false on an empty database. The database decides again when the teacher is written.
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool KeyRequired
        {
            get => _keyRequired;
            set
            {
                _keyRequired = value;
                label5.Visible = value;
                txtAnahtar.Visible = value;
                var prefill = value ? _setup.GetEnvironmentKey() : new SetupKeyPrefill(null, false);
                txtAnahtar.Text = prefill.Key ?? string.Empty;
                lblAciklama.Text = SetupKeyHint(value) + (prefill.Invalid ? "\n" + EnvironmentKeyInvalid : string.Empty);
            }
        }

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

        /// <summary>Who gets the setup key and how; the key itself is never shown here.</summary>
        internal static string SetupKeyHint(bool keyRequired) =>
            keyRequired
                ? "Veritabanında öğrenci hesapları var, öğretmen hesabı yok. İlk öğretmen hesabı yalnızca kurulum anahtarıyla oluşturulur. "
                    + SetupKeySource
                : "Veritabanı boş. İlk öğretmen hesabını oluşturun; şifreyi kendiniz belirlersiniz.";

        /// <summary>Who creates the setup key; shared by the setup and the recovery window.</summary>
        internal const string SetupKeySource =
            "Anahtarı yalnızca veritabanı sahibi docs/issue-setup-key.sql betiğiyle üretir; 24 saat geçerlidir ve bir kez kullanılır.";

        /// <summary>Shown when EGITIM_SETUP_KEY is set to something that is not a setup key.</summary>
        internal const string EnvironmentKeyInvalid =
            "Uyarı: EGITIM_SETUP_KEY ortam değişkeni geçerli bir kurulum anahtarı (32 onaltılık karakter) içermiyor; değişkeni düzeltin veya kaldırın.";

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
