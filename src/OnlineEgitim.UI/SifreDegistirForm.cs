using System.Globalization;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Theming;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// The signed-in account changes its own password with its current one. On success every session of
    /// the account ends: <see cref="Changed"/> tells the panel to return to the sign-in.
    /// </summary>
    public sealed class SifreDegistirForm : AppDialog
    {
        internal const string Done = "Şifreniz değiştirildi. Tüm oturumlarınız kapatıldı; lütfen yeni şifrenizle giriş yapın.";

        private readonly IAuthService _auth;
        private readonly UserSession _session;
        private readonly ExclusiveOperation _change = new();

        private readonly TextBox txtMevcut = new() { Name = nameof(txtMevcut), MaxLength = PasswordPolicy.SignInMaxLength, UseSystemPasswordChar = true };
        private readonly TextBox txtYeni = new() { Name = nameof(txtYeni), MaxLength = PasswordPolicy.MaxLength, UseSystemPasswordChar = true };
        private readonly TextBox txtYeniTekrar = new() { Name = nameof(txtYeniTekrar), MaxLength = PasswordPolicy.MaxLength, UseSystemPasswordChar = true };
        private readonly ThemedButton btnDegistir = FormLayout.Button(nameof(btnDegistir), "Şifreyi değiştir", ButtonKind.Primary);
        private readonly ThemedButton btnIptal = FormLayout.Button(nameof(btnIptal), "İptal", ButtonKind.Secondary);

        public SifreDegistirForm(IAuthService auth, UserSession session)
            : base("Şifremi değiştir", "Mevcut şifrenizle yeni bir şifre belirleyin.", Glyphs.Lock, new Size(520, 440), new Size(440, 400), modal: true)
        {
            ArgumentNullException.ThrowIfNull(auth);
            ArgumentNullException.ThrowIfNull(session);

            _auth = auth;
            _session = session;
            BuildLayout();
            _change.HoldClosingOf(this);
            btnDegistir.Click += async (_, _) => await ChangeAsync();
        }

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

                    // Shown again on the sign-in screen, where the panel returns.
                    Prompts.Inform(Done);
                    DialogResult = DialogResult.OK;
                },
                btnDegistir,
                btnIptal);

        private void BuildLayout()
        {
            Name = nameof(SifreDegistirForm);
            var current = FormLayout.Field("&Mevcut şifre", FormLayout.Input(txtMevcut));
            var next = FormLayout.Field(
                "&Yeni şifre",
                FormLayout.Input(txtYeni),
                string.Create(CultureInfo.CurrentCulture, $"En az {PasswordPolicy.MinLength}, en fazla {PasswordPolicy.MaxLength} karakter."));
            var repeat = FormLayout.Field("Yeni şifre &tekrar", FormLayout.Input(txtYeniTekrar));
            current.Margin = next.Margin = repeat.Margin = new Padding(0, 0, 0, Spacing.M);
            var card = FormLayout.Card("passwordCard", null, FormLayout.Stack("passwordForm", current, next, repeat), fill: false);
            card.Margin = Padding.Empty;
            Content.Controls.Add(card);

            AcceptButton = btnDegistir;
            CancelButton = btnIptal;
            btnIptal.DialogResult = DialogResult.Cancel;
            SetFooterNote("Değişince tüm oturumlarınız kapanır.");
            SetFooterButtons(btnDegistir, btnIptal);
            FinishLayout();
        }
    }
}
