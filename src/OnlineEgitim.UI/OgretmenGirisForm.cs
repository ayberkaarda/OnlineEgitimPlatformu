using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Theming;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// Teacher and administrator sign-in. Closes with <see cref="DialogResult.OK"/> and <see cref="Session"/>
    /// set on success.
    /// </summary>
    public sealed class OgretmenGirisForm : AppDialog
    {
        private readonly IAuthService _auth;
        private readonly ExclusiveOperation _signIn = new();

        private readonly TextBox txtAd = new() { Name = nameof(txtAd), MaxLength = InputRules.PersonNameMaxLength };
        private readonly TextBox txtSifre = new() { Name = nameof(txtSifre), MaxLength = PasswordPolicy.SignInMaxLength, UseSystemPasswordChar = true };
        private readonly ThemedButton btnGiris = FormLayout.Button(nameof(btnGiris), "Giriş Yap", ButtonKind.Primary);
        private readonly ThemedButton btnIptal = FormLayout.Button(nameof(btnIptal), "İptal", ButtonKind.Secondary);

        public OgretmenGirisForm(IAuthService auth)
            : base("Öğretmen / yönetici girişi", string.Empty, string.Empty, new Size(780, 460), new Size(700, 420), modal: true, header: false)
        {
            ArgumentNullException.ThrowIfNull(auth);

            _auth = auth;
            BuildLayout();
            _signIn.HoldClosingOf(this); // a session that is being issued reaches the sign-in screen
            btnGiris.Click += async (_, _) => await SignInAndCloseAsync();
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public UserSession? Session { get; private set; }

        /// <summary>Signs the teacher or administrator in; ignored while a previous attempt is still running.</summary>
        internal async Task<UserSession?> SignInAsync()
        {
            UserSession? session = null;
            await this.RunExclusiveAsync(
                _signIn,
                async () =>
                {
                    try
                    {
                        session = await _auth.SignInTeacherAsync(txtAd.Text, txtSifre.Text);
                    }
                    finally
                    {
                        txtSifre.Clear();
                        if (session is null)
                        {
                            _signIn.FocusAfterUnlock(txtSifre);
                        }
                    }
                },
                btnGiris);
            return session;
        }

        private async Task SignInAndCloseAsync()
        {
            var session = await SignInAsync();
            if (session is not null)
            {
                Session = session;
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void BuildLayout()
        {
            Name = nameof(OgretmenGirisForm);
            SetSidePanel(
                new BrandPanel(
                    "Öğretmen ve yönetici paneli: kurslarınız, notlarınız ve hesaplar.",
                    (Glyphs.Library, "Kurs açın, kontenjanını belirleyin, not ekleyin."),
                    (Glyphs.Key, "Öğrencileriniz için tek kullanımlık kurtarma kodu üretin."),
                    (Glyphs.Admin, "Yöneticiler hesap açar, devre dışı bırakır ve sıfırlar.")),
                Sizes.BrandPanelWidth - 30);

            var title = FormLayout.SectionTitle("Öğretmen / yönetici girişi");
            title.Name = "formTitle";
            title.Font = Theme.Fonts.Title;
            var intro = FormLayout.Hint("Ad soyadınızı ve şifrenizi girin.");
            intro.Name = "formIntro";
            intro.Margin = new Padding(0, 0, 0, Spacing.XL);

            var name = FormLayout.Field("&Ad soyad", FormLayout.Input(txtAd));
            var password = FormLayout.Field("Ş&ifre", FormLayout.Input(txtSifre));
            name.Margin = password.Margin = new Padding(0, 0, 0, Spacing.M);

            var stack = FormLayout.Stack("staffSignInForm", title, intro, name, password);
            stack.Padding = new Padding(Spacing.S, Spacing.S, Spacing.S, 0);
            Content.Controls.Add(stack);

            AcceptButton = btnGiris;
            CancelButton = btnIptal;
            btnIptal.DialogResult = DialogResult.Cancel;
            SetFooterNote("Öğrenci girişi için bu pencereyi kapatın.");
            SetFooterButtons(btnGiris, btnIptal);
            FinishLayout();
        }
    }
}
