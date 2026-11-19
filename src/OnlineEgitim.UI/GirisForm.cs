using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
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
        private SetupStage _stage = SetupStage.Completed;

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
            Stage = SetupStage.Completed;
        }

        /// <summary>The signed-in student or teacher, once the form closed with OK.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public UserSession? Session { get; private set; }

        /// <summary>
        /// Where the setup of the database stands. The link to the first-run setup is shown only while
        /// students exist but no teacher (<see cref="SetupStage.FirstTeacherNeedsKey"/>), and the teacher
        /// recovery with the setup key only while no teacher can sign in with a password. The database
        /// decides again when the key is used.
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal SetupStage Stage
        {
            get => _stage;
            set
            {
                _stage = value;
                lnkKurulum.Visible = OffersFirstTeacherSetup;
            }
        }

        /// <summary>Whether the link to the first-run setup (with the setup key) is shown.</summary>
        internal bool OffersFirstTeacherSetup => _stage == SetupStage.FirstTeacherNeedsKey;

        /// <summary>Whether the recovery window offers the founder's recovery with the setup key.</summary>
        internal bool OffersSetupKeyRecovery => _stage is SetupStage.FirstTeacherNeedsKey or SetupStage.TeacherPasswordNeedsKey;

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
                lnkKurulum,
                lnkKurtarma);
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

        private void lnkKurtarma_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
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

        private void button1_Click(object? sender, EventArgs e)
        {
            using var register = _forms.Create<KayitForm>();
            register.ShowDialog(this);
        }
    }
}
