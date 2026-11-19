using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>Teacher sign-in. Closes with <see cref="DialogResult.OK"/> and <see cref="Session"/> set on success.</summary>
    public partial class OgretmenGirisForm : Form
    {
        private readonly IAuthService _auth;
        private readonly ExclusiveOperation _signIn = new();

        public OgretmenGirisForm(IAuthService auth)
        {
            ArgumentNullException.ThrowIfNull(auth);

            _auth = auth;
            Prompts = new MessageBoxPrompts(this);

            InitializeComponent();
            Icon = AppAssets.Icon;
            Text = AppText.Title + " - " + Text;
            txtAd.MaxLength = InputRules.PersonNameMaxLength;
            txtSifre.MaxLength = PasswordPolicy.SignInMaxLength;
            AcceptButton = btnGiris;
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public UserSession? Session { get; private set; }

        /// <summary>Information dialogs; replaced in tests so no window opens.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal IUserPrompts Prompts { get; set; }

        /// <summary>Signs the teacher in; ignored while a previous attempt is still running.</summary>
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

        private async void btnGiris_Click(object? sender, EventArgs e)
        {
            var session = await SignInAsync();
            if (session is not null)
            {
                Prompts.Inform("Giriş başarılı!");
                Session = session;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}
