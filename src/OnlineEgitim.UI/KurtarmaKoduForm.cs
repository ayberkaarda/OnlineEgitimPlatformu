using System.Globalization;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// Lets the signed-in teacher issue a single use recovery code for a student account without a
    /// password. The code is shown once in this window, never logged; only its hash is stored.
    /// </summary>
    public partial class KurtarmaKoduForm : Form
    {
        private readonly IRecoveryService _recovery;
        private readonly UserSession _session;
        private readonly ExclusiveOperation _issue = new();

        public KurtarmaKoduForm(IRecoveryService recovery, UserSession session)
        {
            ArgumentNullException.ThrowIfNull(recovery);
            ArgumentNullException.ThrowIfNull(session);

            _recovery = recovery;
            _session = session;

            InitializeComponent();
            _issue.HoldClosingOf(this);
            Icon = AppAssets.Icon;
            Text = AppText.Title + " - " + Text;
            txtAd.MaxLength = InputRules.PersonNameMaxLength;
            txtKod.TabStop = false;
            AcceptButton = btnUret;
            CancelButton = btnKapat;
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

        private async void btnUret_Click(object? sender, EventArgs e) => await IssueAsync();
    }
}
