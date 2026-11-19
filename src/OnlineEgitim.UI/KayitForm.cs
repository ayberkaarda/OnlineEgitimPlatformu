using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>Student registration.</summary>
    public partial class KayitForm : Form
    {
        private readonly IAuthService _auth;
        private readonly ExclusiveOperation _save = new();

        public KayitForm(IAuthService auth)
        {
            ArgumentNullException.ThrowIfNull(auth);

            _auth = auth;
            Prompts = new MessageBoxPrompts(this);

            InitializeComponent();
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

                    Prompts.Inform("Kayıt başarıyla tamamlandı!");
                    txtAd.Clear();
                    txtMail.Clear();
                    _save.FocusAfterUnlock(txtAd);
                },
                btnKaydet);

        private async void btnKaydet_Click(object? sender, EventArgs e) => await SaveAsync();

        private void btnIptal_Click(object? sender, EventArgs e) => Close();
    }
}
