using System.Globalization;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Theming;

namespace OnlineEgitim.UI
{
    /// <summary>Student registration.</summary>
    public sealed class KayitForm : AppDialog
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

        private readonly TextBox txtAd = new() { Name = nameof(txtAd), MaxLength = InputRules.PersonNameMaxLength };
        private readonly TextBox txtMail = new() { Name = nameof(txtMail), MaxLength = InputRules.EmailMaxLength };
        private readonly TextBox txtSifre = new() { Name = nameof(txtSifre), MaxLength = PasswordPolicy.MaxLength, UseSystemPasswordChar = true };
        private readonly ThemedButton btnKaydet = FormLayout.Button(nameof(btnKaydet), "Kayıt ol", ButtonKind.Primary);

        public KayitForm(IAuthService auth)
            : base("Öğrenci kaydı", "Ad soyad, e-posta adresi ve bir şifreyle hesap açın.", Glyphs.AddPerson, new Size(560, 430), new Size(460, 400), modal: true)
        {
            ArgumentNullException.ThrowIfNull(auth);

            _auth = auth;
            BuildLayout();
            _save.HoldClosingOf(this);
            btnKaydet.Click += async (_, _) => await SaveAsync();
        }

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

        private void BuildLayout()
        {
            Name = nameof(KayitForm);
            var name = FormLayout.Field("&Ad soyad", FormLayout.Input(txtAd), "Giriş adınız olur; harf, boşluk, kesme işareti, nokta ve kısa çizgi kullanılabilir.");
            var mail = FormLayout.Field("&E-posta", FormLayout.Input(txtMail));
            var password = FormLayout.Field(
                "Ş&ifre",
                FormLayout.Input(txtSifre),
                string.Create(CultureInfo.CurrentCulture, $"En az {PasswordPolicy.MinLength}, en fazla {PasswordPolicy.MaxLength} karakter."));
            name.Margin = mail.Margin = password.Margin = new Padding(0, 0, 0, Spacing.M);
            var card = FormLayout.Card("registrationCard", null, FormLayout.Stack("registrationForm", name, mail, password), fill: false);
            card.Margin = Padding.Empty;
            Content.Controls.Add(card);

            AcceptButton = btnKaydet;
            SetFooterNote("Kayıttan sonra giriş ekranından giriş yapın.");
            SetFooterButtons(btnKaydet, CreateCloseButton("btnIptal"));
            FinishLayout();
        }
    }
}
