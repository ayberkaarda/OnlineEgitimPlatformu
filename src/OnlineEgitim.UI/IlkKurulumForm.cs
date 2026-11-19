using System.Globalization;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Theming;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// First-run setup: opens by itself on an empty database (no key needed) and from the sign-in while
    /// accounts exist but no administrator and no teacher who signs in with a password, where the setup
    /// key created in the database is required. The person installing the application creates the
    /// first administrator account with a password of their own; there is no default password. Closes
    /// with <see cref="DialogResult.OK"/> and <see cref="Session"/> set on success.
    /// </summary>
    public sealed class IlkKurulumForm : AppDialog
    {
        private readonly ISetupService _setup;
        private readonly ExclusiveOperation _create = new();
        private bool _keyRequired;

        private readonly Label lblAciklama = FormLayout.Text(nameof(lblAciklama));
        private readonly TextBox txtAnahtar = new() { Name = nameof(txtAnahtar), MaxLength = 64, UseSystemPasswordChar = true };
        private readonly TextBox txtAd = new() { Name = nameof(txtAd), MaxLength = InputRules.PersonNameMaxLength };
        private readonly TextBox txtUzmanlik = new() { Name = nameof(txtUzmanlik), MaxLength = InputRules.SpecialtyMaxLength };
        private readonly TextBox txtSifre = new() { Name = nameof(txtSifre), MaxLength = PasswordPolicy.MaxLength, UseSystemPasswordChar = true };
        private readonly TextBox txtSifreTekrar = new() { Name = nameof(txtSifreTekrar), MaxLength = PasswordPolicy.MaxLength, UseSystemPasswordChar = true };
        private readonly ThemedButton btnOlustur = FormLayout.Button(nameof(btnOlustur), "Yöneticiyi oluştur", ButtonKind.Primary);
        private readonly ThemedButton btnIptal = FormLayout.Button(nameof(btnIptal), "İptal", ButtonKind.Secondary);
        private TableLayoutPanel _keyField = null!;

        public IlkKurulumForm(ISetupService setup)
            : base("İlk kurulum", "İlk yönetici hesabını kendi şifrenizle oluşturun.", Glyphs.Admin, new Size(640, 560), new Size(520, 480))
        {
            ArgumentNullException.ThrowIfNull(setup);

            _setup = setup;
            BuildLayout();
            _create.HoldClosingOf(this);
            KeyRequired = true;
            btnOlustur.Click += async (_, _) => await CreateAndCloseAsync();
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public UserSession? Session { get; private set; }

        /// <summary>
        /// Whether the setup key field is shown: true from the sign-in (the database has accounts),
        /// false on an empty database. The database decides again when the administrator is written.
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool KeyRequired
        {
            get => _keyRequired;
            set
            {
                _keyRequired = value;
                FormLayout.SetShown(_keyField, value);
                var prefill = value ? _setup.GetEnvironmentKey() : new SetupKeyPrefill(null, false);
                txtAnahtar.Text = prefill.Key ?? string.Empty;
                lblAciklama.Text = SetupKeyHint(value) + (prefill.Invalid ? "\n" + EnvironmentKeyInvalid : string.Empty);
            }
        }

        /// <summary>Creates the first administrator; ignored while a previous attempt is still running.</summary>
        internal async Task<UserSession?> CreateAsync()
        {
            UserSession? session = null;
            await this.RunExclusiveAsync(
                _create,
                async () =>
                {
                    try
                    {
                        session = await _setup.CreateFirstAdminAsync(txtAnahtar.Text, txtAd.Text, txtUzmanlik.Text, txtSifre.Text, txtSifreTekrar.Text);
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
                ? "Veritabanında hesaplar var, yönetici hesabı yok. İlk yönetici hesabı yalnızca kurulum anahtarıyla oluşturulur. "
                    + SetupKeySource
                : "Veritabanı boş. İlk yönetici hesabını oluşturun; şifreyi kendiniz belirlersiniz. Öğretmen ve öğrenci hesaplarını sonra yönetici panelinden açarsınız.";

        /// <summary>Who creates the setup key; shared by the setup and the recovery window.</summary>
        internal const string SetupKeySource =
            "Anahtarı yalnızca veritabanı sahibi docs/issue-setup-key.sql betiğiyle üretir; 24 saat geçerlidir ve bir kez kullanılır.";

        /// <summary>Shown when EGITIM_SETUP_KEY is set to something that is not a setup key.</summary>
        internal const string EnvironmentKeyInvalid =
            "Uyarı: EGITIM_SETUP_KEY ortam değişkeni geçerli bir kurulum anahtarı (32 onaltılık karakter) içermiyor; değişkeni düzeltin veya kaldırın.";

        private async Task CreateAndCloseAsync()
        {
            var session = await CreateAsync();
            if (session is not null)
            {
                Session = session;
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void BuildLayout()
        {
            Name = nameof(IlkKurulumForm);
            var note = FormLayout.Note("setupNote", Glyphs.Info, lblAciklama);

            _keyField = FormLayout.Field("&Kurulum anahtarı", FormLayout.Input(txtAnahtar), "Anahtar ekranda gösterilmez; yapıştırabilirsiniz.");
            var name = FormLayout.Field("&Ad soyad", FormLayout.Input(txtAd));
            var specialty = FormLayout.Field("&Uzmanlık alanı", FormLayout.Input(txtUzmanlik));
            var passwords = FormLayout.FieldRow(
                "passwordRow",
                (FormLayout.Field("Ş&ifre", FormLayout.Input(txtSifre), string.Create(CultureInfo.CurrentCulture, $"En az {PasswordPolicy.MinLength} karakter.")), 50),
                (FormLayout.Field("Şifre &tekrar", FormLayout.Input(txtSifreTekrar)), 50));
            _keyField.Margin = name.Margin = specialty.Margin = new Padding(0, 0, 0, Spacing.M);

            var form = FormLayout.Stack("setupForm", _keyField, name, specialty, passwords);
            var card = FormLayout.Card("setupCard", null, form, fill: false);
            card.Margin = Padding.Empty;
            Content.Controls.Add(FormLayout.Page("setupPage", (note, false), (card, false)));

            AcceptButton = btnOlustur;
            CancelButton = btnIptal;
            btnIptal.DialogResult = DialogResult.Cancel;
            SetFooterNote("Hesap oluşunca yönetici paneli açılır.");
            SetFooterButtons(btnOlustur, btnIptal);
            FinishLayout();
        }
    }
}
