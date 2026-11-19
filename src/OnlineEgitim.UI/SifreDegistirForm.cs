using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// The signed-in account changes its own password with its current one. On success every session of
    /// the account ends: <see cref="Changed"/> tells the panel to return to the sign-in.
    /// </summary>
    public partial class SifreDegistirForm : Form
    {
        internal const string Done = "Şifreniz değiştirildi. Tüm oturumlarınız kapatıldı; lütfen yeni şifrenizle giriş yapın.";

        private readonly IAuthService _auth;
        private readonly UserSession _session;
        private readonly ExclusiveOperation _change = new();

        public SifreDegistirForm(IAuthService auth, UserSession session)
        {
            ArgumentNullException.ThrowIfNull(auth);
            ArgumentNullException.ThrowIfNull(session);

            _auth = auth;
            _session = session;
            Prompts = new MessageBoxPrompts(this);

            InitializeComponent();
            _change.HoldClosingOf(this);
            Icon = AppAssets.Icon;
            Text = AppText.Title + " - " + Text;
            txtMevcut.MaxLength = PasswordPolicy.SignInMaxLength;
            txtYeni.MaxLength = PasswordPolicy.MaxLength;
            txtYeniTekrar.MaxLength = PasswordPolicy.MaxLength;
            AcceptButton = btnDegistir;
            CancelButton = btnIptal;
        }

        /// <summary>Information dialogs; replaced in tests so no window opens.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal IUserPrompts Prompts { get; set; }

        /// <summary>Whether the password was changed (and the account's sessions ended).</summary>
        internal bool Changed { get; private set; }

        /// <summary>Changes the password; ignored while a previous attempt is still running. The fields are always cleared.</summary>
        internal Task<bool> ChangeAsync() =>
            this.RunExclusiveAsync(
                _change,
                async () =>
                {
                    try
                    {
                        await _auth.ChangePasswordAsync(_session, txtMevcut.Text, txtYeni.Text, txtYeniTekrar.Text);
                        Changed = true;
                    }
                    finally
                    {
                        txtMevcut.Clear();
                        txtYeni.Clear();
                        txtYeniTekrar.Clear();
                        if (!Changed)
                        {
                            _change.FocusAfterUnlock(txtMevcut);
                        }
                    }

                    Prompts.Inform(Done);
                    DialogResult = DialogResult.OK;
                },
                btnDegistir,
                btnIptal);

        private async void btnDegistir_Click(object? sender, EventArgs e) => await ChangeAsync();
    }
}
