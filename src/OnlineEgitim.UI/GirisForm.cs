using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Theming;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// Student sign-in; also the entry point to the staff (teacher and administrator) sign-in, to the
    /// student registration and to setting a password with a code.
    /// Closes with <see cref="DialogResult.OK"/> and <see cref="Session"/> set once someone signed in.
    /// </summary>
    public sealed class GirisForm : AppDialog
    {
        private readonly IAuthService _auth;
        private readonly IFormFactory _forms;
        private readonly ExclusiveOperation _signIn = new();
        private SetupStage _stage = SetupStage.Completed;

        private readonly TextBox txtAd = new() { Name = nameof(txtAd), MaxLength = InputRules.PersonNameMaxLength };
        private readonly TextBox txtSifre = new() { Name = nameof(txtSifre), MaxLength = PasswordPolicy.SignInMaxLength, UseSystemPasswordChar = true };
        private readonly ThemedButton btnOgrenciGiris = FormLayout.Button(nameof(btnOgrenciGiris), "Giriş Yap", ButtonKind.Primary);
        private readonly ThemedButton btnKayit = FormLayout.Button(nameof(btnKayit), "Kayıt ol", ButtonKind.Secondary, Glyphs.AddPerson);
        private readonly ThemedButton btnKurtarma = FormLayout.Button(nameof(btnKurtarma), "Kodla şifre belirle", ButtonKind.Subtle, Glyphs.Key);
        private readonly ThemedButton btnKurulum = FormLayout.Button(nameof(btnKurulum), "İlk yönetici kurulumu (kurulum anahtarı)", ButtonKind.Subtle, Glyphs.Admin);
        private readonly ThemedButton btnOgretmenGiris = FormLayout.Button(nameof(btnOgretmenGiris), "Öğretmen / yönetici girişi", ButtonKind.Subtle, Glyphs.Person);

        public GirisForm(IAuthService auth, IFormFactory forms)
            : base("Giriş", string.Empty, string.Empty, new Size(860, 540), new Size(760, 500), header: false)
        {
            ArgumentNullException.ThrowIfNull(auth);
            ArgumentNullException.ThrowIfNull(forms);

            _auth = auth;
            _forms = forms;
            BuildLayout();
            Stage = SetupStage.Completed;

            btnOgrenciGiris.Click += async (_, _) => await SignInAndCloseAsync();
            btnOgretmenGiris.Click += (_, _) => OpenStaffSignIn();
            btnKayit.Click += (_, _) => OpenRegistration();
            btnKurtarma.Click += (_, _) => OpenRecovery();
            btnKurulum.Click += (_, _) => OpenSetup();
        }

        /// <summary>The signed-in student, teacher or administrator, once the form closed with OK.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public UserSession? Session { get; private set; }

        /// <summary>
        /// Where the setup of the database stands. The way to the first administrator's setup (with the
        /// setup key) and the recovery with the setup key are offered only while no teacher or
        /// administrator can sign in with a password. The database decides again when the key is used.
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal SetupStage Stage
        {
            get => _stage;
            set
            {
                _stage = value;
                FormLayout.SetShown(btnKurulum, OffersFirstAdminSetup);
            }
        }

        /// <summary>Whether the way to the first administrator's setup (with the setup key) is shown.</summary>
        internal bool OffersFirstAdminSetup => _stage is SetupStage.FirstAdminNeedsKey or SetupStage.StaffPasswordNeedsKey;

        /// <summary>Whether the recovery window offers the founder's recovery with the setup key.</summary>
        internal bool OffersSetupKeyRecovery => _stage is SetupStage.FirstAdminNeedsKey or SetupStage.StaffPasswordNeedsKey;

        /// <summary>A message from the session that just ended, for example after a password change.</summary>
        internal void ShowNotice(string? notice)
        {
            if (!string.IsNullOrWhiteSpace(notice))
            {
                ShowStatus(StatusKind.Info, notice);
            }
        }

        /// <summary>
        /// Signs the student in; ignored while a previous attempt is still running. Returns the session,
        /// or <see langword="null"/> when the attempt failed (the reason has been shown) or was ignored.
        /// </summary>
        internal async Task<UserSession?> SignInAsync()
        {
            UserSession? session = null;
            await this.RunExclusiveAsync(
                _signIn,
                async () =>
                {
                    try
                    {
                        session = await _auth.SignInStudentAsync(txtAd.Text, txtSifre.Text);
                    }
                    finally
                    {
                        txtSifre.Clear();
                        if (session is null)
                        {
                            // Retyping the password is the next step after a refused attempt.
                            _signIn.FocusAfterUnlock(txtSifre);
                        }
                    }
                },
                btnOgrenciGiris,
                btnOgretmenGiris,
                btnKayit,
                btnKurulum,
                btnKurtarma);
            return session;
        }

        private async Task SignInAndCloseAsync()
        {
            var session = await SignInAsync();
            if (session is not null)
            {
                Complete(session);
            }
        }

        private void Complete(UserSession session)
        {
            Session = session;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OpenStaffSignIn()
        {
            using var staffSignIn = _forms.Create<OgretmenGirisForm>();
            if (staffSignIn.ShowDialog(this) == DialogResult.OK && staffSignIn.Session is { } session)
            {
                Complete(session);
            }
        }

        private void OpenSetup()
        {
            if (_signIn.IsRunning)
            {
                return;
            }

            using var setup = _forms.Create<IlkKurulumForm>();
            if (setup.ShowDialog(this) == DialogResult.OK && setup.Session is { } session)
            {
                Complete(session);
            }
        }

        private void OpenRecovery()
        {
            if (_signIn.IsRunning)
            {
                return;
            }

            using var recovery = _forms.Create<HesapKurtarmaForm>();
            recovery.SetupKeyAccepted = OffersSetupKeyRecovery;
            if (recovery.ShowDialog(this) == DialogResult.OK && recovery.Session is { } session)
            {
                Complete(session);
            }
        }

        private void OpenRegistration()
        {
            using var register = _forms.Create<KayitForm>();
            register.ShowDialog(this);
        }

        private void BuildLayout()
        {
            Name = nameof(GirisForm);
            SetSidePanel(
                new BrandPanel(
                    "Kurslara katılın, notları izleyin; öğrenmeye kaldığınız yerden devam edin.",
                    (Glyphs.Search, "Kurs kataloğunda ada, konuya ya da öğretmene göre arayın."),
                    (Glyphs.Join, "Boş yeri olan kurslara tek adımda kaydolun."),
                    (Glyphs.Note, "Kayıtlı olduğunuz kursların notlarını görün.")),
                Sizes.BrandPanelWidth);

            var title = FormLayout.SectionTitle("Öğrenci girişi");
            title.Name = "formTitle";
            title.Font = Theme.Fonts.Title;
            var intro = FormLayout.Hint("Ad soyadınız ve şifrenizle giriş yapın.");
            intro.Name = "formIntro";
            intro.Margin = new Padding(0, 0, 0, Spacing.XL);

            var name = FormLayout.Field("&Ad soyad", FormLayout.Input(txtAd));
            var password = FormLayout.Field("Ş&ifre", FormLayout.Input(txtSifre));
            name.Margin = password.Margin = new Padding(0, 0, 0, Spacing.M);
            btnOgrenciGiris.AutoSize = false;
            btnOgrenciGiris.Dock = DockStyle.Top;
            btnOgrenciGiris.Margin = new Padding(0, Spacing.S, 0, Spacing.XL);

            var newHere = FormLayout.Hint("Hesabınız yok mu? Birkaç bilgiyle hemen kayıt olun.");
            newHere.Name = "registerHint";
            newHere.Margin = new Padding(0, 0, 0, Spacing.S);
            btnKayit.Margin = new Padding(0, 0, 0, Spacing.L);

            var noPassword = FormLayout.Hint("Şifreniz yok ya da unuttunuz mu? Öğretmeninizden veya yöneticiden aldığınız kodla yeni şifre belirleyin.");
            noPassword.Name = "recoveryHint";
            noPassword.Margin = new Padding(0, 0, 0, Spacing.XS);
            btnKurtarma.Margin = btnKurulum.Margin = Padding.Empty;

            var stack = FormLayout.Stack("signInForm", title, intro, name, password, btnOgrenciGiris, newHere, btnKayit, noPassword, btnKurtarma, btnKurulum);
            stack.Padding = new Padding(Spacing.S, Spacing.S, Spacing.S, 0);
            btnKayit.Anchor = btnKurtarma.Anchor = btnKurulum.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            Content.Controls.Add(stack);

            AcceptButton = btnOgrenciGiris;
            SetFooterNote("Öğretmenler ve yöneticiler kendi girişlerini kullanır.");
            SetFooterButtons(btnOgretmenGiris, CreateCloseButton("btnCikis", "Çıkış"));
            FinishLayout();
        }
    }
}
