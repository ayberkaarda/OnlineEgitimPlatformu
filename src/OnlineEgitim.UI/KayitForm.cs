using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>Student registration.</summary>
    public partial class KayitForm : Form
    {
        /// <summary>
        /// The one answer to every accepted registration: a new account and a taken name or e-mail
        /// address look the same, so the form does not tell whether someone is registered.
        /// </summary>
        internal const string RegistrationReceived =
            "Kayıt isteğiniz işlendi. Ad ve e-posta adresiniz daha önce kullanılmadıysa hesabınız oluşturuldu; "
            + "seçtiğiniz şifreyle giriş yapabilirsiniz. Giriş yapamıyorsanız bu ad veya e-posta adresi başka bir hesapta "
            + "kullanılıyor olabilir; farklı bilgilerle yeniden kayıt olun.";

        private readonly IAuthService _auth;
        private readonly ExclusiveOperation _save = new();

        public KayitForm(IAuthService auth)
        {
            ArgumentNullException.ThrowIfNull(auth);

            _auth = auth;
            Prompts = new MessageBoxPrompts(this);

            InitializeComponent();
            _save.HoldClosingOf(this);
            Icon = AppAssets.Icon;
            Text = AppText.Title + " - " + Text;
            txtAd.MaxLength = InputRules.PersonNameMaxLength;
            txtMail.MaxLength = InputRules.EmailMaxLength;
            txtSifre.MaxLength = PasswordPolicy.MaxLength;
            AcceptButton = btnKaydet;
            CancelButton = btnIptal;
        }

        /// <summary>Information dialogs; replaced in tests so no window opens.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal IUserPrompts Prompts { get; set; }

        /// <summary>Registers the student; ignored while a previous registration is still running.</summary>
        internal Task<bool> SaveAsync() =>
            this.RunExclusiveAsync(
                _save,
                async () =>
                {
                    try
                    {
                        await _auth.RegisterStudentAsync(txtAd.Text, txtMail.Text, txtSifre.Text);
                    }
                    finally
                    {
                        txtSifre.Clear();
                    }

                    Prompts.Inform(RegistrationReceived);
                    txtAd.Clear();
                    txtMail.Clear();
                    _save.FocusAfterUnlock(txtAd);
                },
                btnKaydet);

        private async void btnKaydet_Click(object? sender, EventArgs e) => await SaveAsync();

        private void btnIptal_Click(object? sender, EventArgs e) => Close();
    }
}
