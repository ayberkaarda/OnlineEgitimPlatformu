using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// Student sign-in; also the entry point to the teacher sign-in and to the student registration.
    /// Closes with <see cref="DialogResult.OK"/> and <see cref="Session"/> set once someone signed in.
    /// </summary>
    public partial class GirisForm : Form
    {
        private readonly IAuthService _auth;
        private readonly IFormFactory _forms;
        private readonly ExclusiveOperation _signIn = new();
        private bool _offerTeacherSetup;

        public GirisForm(IAuthService auth, IFormFactory forms)
        {
            ArgumentNullException.ThrowIfNull(auth);
            ArgumentNullException.ThrowIfNull(forms);

            _auth = auth;
            _forms = forms;
            Prompts = new MessageBoxPrompts(this);

            InitializeComponent();
            Icon = AppAssets.Icon;
            Text = AppText.Title + " - " + Text;
            pictureBox1.Image = AppAssets.Background;
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox2.Image = AppAssets.Logo;
            pictureBox2.SizeMode = PictureBoxSizeMode.Zoom;
            txtAd.MaxLength = InputRules.PersonNameMaxLength;
            txtSifre.MaxLength = PasswordPolicy.SignInMaxLength;
            AcceptButton = btnOgrenciGiris;
        }

        /// <summary>The signed-in student or teacher, once the form closed with OK.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public UserSession? Session { get; private set; }

        /// <summary>
        /// Shows the link to the first-run setup; set while no teacher exists. The setup itself still
        /// needs the setup key.
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool OfferTeacherSetup
        {
            get => _offerTeacherSetup;
            set
            {
                _offerTeacherSetup = value;
                lnkKurulum.Visible = value;
            }
        }

        /// <summary>Information dialogs; replaced in tests so no window opens.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal IUserPrompts Prompts { get; set; }

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
                button1,
                lnkKurulum);
            return session;
        }

        private void Complete(UserSession session)
        {
            Session = session;
            DialogResult = DialogResult.OK;
            Close();
        }

        private async void btnOgrenciGiris_Click(object? sender, EventArgs e)
        {
            var session = await SignInAsync();
            if (session is not null)
            {
                Prompts.Inform("Giriş başarılı!");
                Complete(session);
            }
        }

        private void btnOgretmenGiris_Click(object? sender, EventArgs e)
        {
            using var teacherSignIn = _forms.Create<OgretmenGirisForm>();
            if (teacherSignIn.ShowDialog(this) == DialogResult.OK && teacherSignIn.Session is { } session)
            {
                Complete(session);
            }
        }

        private void lnkKurulum_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
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

        private void button1_Click(object? sender, EventArgs e)
        {
            using var register = _forms.Create<KayitForm>();
            register.ShowDialog(this);
        }
    }
}
