using System.Globalization;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Theming;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// Lets the signed-in teacher issue a single use recovery code for a student of their courses: a
    /// first password, or a new one for a student who forgot theirs. The code is shown once in this window, never logged; only its hash is stored.
    /// </summary>
    public sealed class KurtarmaKoduForm : AppDialog
    {
        internal const string Explanation =
            "Kod yalnızca kendi kurslarınızdan birine kayıtlı öğrenci için üretilir, 24 saat geçerlidir ve bir kez kullanılır. "
            + "Öğrenci giriş ekranındaki \"Kodla şifre belirle\" ile yeni şifresini belirler; kod burada bir kez gösterilir.";

        private readonly IRecoveryService _recovery;
        private readonly UserSession _session;
        private readonly ExclusiveOperation _issue = new();

        private readonly Label lblAciklama = FormLayout.Text(nameof(lblAciklama), Explanation);
        private readonly TextBox txtAd = new() { Name = nameof(txtAd), MaxLength = InputRules.PersonNameMaxLength };
        private readonly ThemedButton btnUret = FormLayout.Button(nameof(btnUret), "Kod üret", ButtonKind.Primary, Glyphs.Key);
        private readonly TextBox txtKod = new() { Name = nameof(txtKod), TabStop = false, PlaceholderText = "Kod burada gösterilir" };
        private readonly Label lblBitis = FormLayout.Hint(string.Empty);
        private readonly ThemedButton btnKapat = FormLayout.Button(nameof(btnKapat), "Kapat", ButtonKind.Secondary);

        public KurtarmaKoduForm(IRecoveryService recovery, UserSession session)
            : base("Kurtarma kodu", "Kursunuzdaki bir öğrenci için tek kullanımlık şifre kodu üretin.", Glyphs.Key, new Size(600, 500), new Size(500, 440), modal: true)
        {
            ArgumentNullException.ThrowIfNull(recovery);
            ArgumentNullException.ThrowIfNull(session);

            _recovery = recovery;
            _session = session;
            BuildLayout();
            _issue.HoldClosingOf(this);
            btnUret.Click += async (_, _) => await IssueAsync();
        }

        /// <summary>Issues the code; ignored while a previous request is still running.</summary>
        internal Task<bool> IssueAsync() =>
            this.RunExclusiveAsync(
                _issue,
                async () =>
                {
                    txtKod.Clear();
                    lblBitis.Text = string.Empty;
                    var code = await _recovery.IssueStudentCodeAsync(_session, txtAd.Text);
                    txtKod.Text = code.Code;
                    lblBitis.Text = "Son geçerlilik: " + code.ExpiresUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
                    _issue.FocusAfterUnlock(txtKod);
                },
                btnUret,
                btnKapat);

        private void BuildLayout()
        {
            Name = nameof(KurtarmaKoduForm);
            var note = FormLayout.Note("codeNote", Glyphs.Info, lblAciklama);

            btnUret.Margin = Padding.Empty;
            var student = FormLayout.FieldRow(
                "studentRow",
                (FormLayout.Field("Öğrencinin &adı soyadı", FormLayout.Input(txtAd)), 100),
                (FormLayout.FieldAction(btnUret), 0));
            var requestCard = FormLayout.Card("requestCard", null, student, fill: false);

            var codeField = FormLayout.Field("Tek kullanımlık kod", FormLayout.CodeInput(txtKod));
            codeField.Margin = new Padding(0, 0, 0, Spacing.XS);
            lblBitis.Name = nameof(lblBitis);
            lblBitis.Margin = Padding.Empty;
            var codeCard = FormLayout.Card("codeCard", null, FormLayout.Stack("codeStack", codeField, lblBitis), fill: false);
            codeCard.Edge = Theme.Current.Warm;
            codeCard.Margin = Padding.Empty;

            Content.Controls.Add(FormLayout.Page("codePage", (note, false), (requestCard, false), (codeCard, false)));

            AcceptButton = btnUret;
            CancelButton = btnKapat;
            btnKapat.Click += (_, _) => Close();
            SetFooterNote("Kodu öğrenciye kendiniz iletin; uygulama yalnızca özetini saklar.");
            SetFooterButtons(btnKapat);
            FinishLayout();
        }
    }
}
