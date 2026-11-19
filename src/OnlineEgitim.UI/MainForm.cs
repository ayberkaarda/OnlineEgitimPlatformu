using System.Globalization;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// Student panel: the course catalogue with search, filters and pages, course details, joining and
    /// leaving a course and the student's own courses.
    /// </summary>
    public partial class MainForm : Form
    {
        internal static string LeaveQuestion(string courseName) =>
            $"\"{courseName}\" dersinden ayrılmak istiyor musunuz? Ayrıldığınızda dersin notlarını göremezsiniz ve aynı gün yeniden kaydolamazsınız.";

        private readonly ICourseService _courses;
        private readonly IEnrollmentService _enrollments;
        private readonly IFormFactory _forms;
        private readonly UserSession _session;
        private readonly ExclusiveOperation _write = new();
        private readonly RequestSequence _searchRequests = new();
        private readonly bool _ready;
        private int _pageIndex;

        public MainForm(ICourseService courses, IEnrollmentService enrollments, IFormFactory forms, UserSession session)
        {
            ArgumentNullException.ThrowIfNull(courses);
            ArgumentNullException.ThrowIfNull(enrollments);
            ArgumentNullException.ThrowIfNull(forms);
            ArgumentNullException.ThrowIfNull(session);

            _courses = courses;
            _enrollments = enrollments;
            _forms = forms;
            _session = session;
            Prompts = new MessageBoxPrompts(this);

            InitializeComponent();
            Icon = AppAssets.Icon;
            Text = AppText.Title + " - " + Text;
            lblhosgeldin.Text = $"Hoş geldin, {session.DisplayName}!";
            txtAra.MaxLength = InputRules.CourseNameMaxLength;
            txtKonu.MaxLength = InputRules.TopicMaxLength;
            txtOgretmen.MaxLength = InputRules.PersonNameMaxLength;
            cmbKayit.Items.AddRange(["Tümü", "Kayıtlı olduklarım", "Kayıtlı olmadıklarım"]);
            cmbKayit.SelectedIndex = 0; // set before _ready: not a choice of the user, no search
            _ready = true;
            Load += async (_, _) => await LoadAsync();
        }

        /// <summary>Information dialogs; replaced in tests so no window opens.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal IUserPrompts Prompts { get; set; }

        /// <summary>The search the filters describe, for page <paramref name="pageIndex"/>.</summary>
        internal CourseSearch CurrentSearch(int pageIndex) => new(
            txtAra.Text,
            txtKonu.Text,
            txtOgretmen.Text,
            chkBosKontenjan.Checked,
            cmbKayit.SelectedIndex switch
            {
                1 => EnrollmentFilter.Enrolled,
                2 => EnrollmentFilter.NotEnrolled,
                _ => EnrollmentFilter.Any,
            },
            pageIndex);

        /// <summary>Lists the first page of the catalogue.</summary>
        internal Task LoadAsync() => SearchAsync(0);

        /// <summary>
        /// Searches the catalogue. Searches can be started faster than the answers arrive (typing, paging,
        /// filters), so only the answer to the latest search is shown; refilling the list selects nothing.
        /// </summary>
        internal async Task SearchAsync(int pageIndex)
        {
            var request = _searchRequests.Next();
            var search = CurrentSearch(pageIndex);
            await this.RunAsync(async () =>
            {
                var result = await _courses.SearchCatalogAsync(_session, search);
                if (!_searchRequests.IsLatest(request) || IsDisposed)
                {
                    return;
                }

                _pageIndex = result.PageIndex;
                lstKurslar.ReplaceItems(result.Items.Select(e => new CourseItem(e.Course, CourseItemStyle.Catalog, e.Enrolled)));
                lstKurslar.ClearSelected();
                lblSayfa.Text = string.Create(
                    CultureInfo.CurrentCulture,
                    $"Sayfa {result.PageIndex + 1} / {result.PageCount} ({result.TotalCount} kurs)");
                btnOnceki.Enabled = result.PageIndex > 0;
                btnSonraki.Enabled = result.PageIndex + 1 < result.PageCount;
            });
        }

        /// <summary>Joins the selected course; ignored while a previous join or leave is still running.</summary>
        internal async Task<bool> EnrollAsync()
        {
            var changed = false;
            var ran = await this.RunExclusiveAsync(
                _write,
                async () =>
                {
                    if (lstKurslar.SelectedItem is not CourseItem item)
                    {
                        Prompts.Warn("Lütfen bir ders seçiniz.");
                        return;
                    }

                    await _enrollments.EnrollAsync(_session, item.Course.Id);
                    changed = true;
                    Prompts.Inform("Ders başarıyla seçildi!");
                },
                btnDersSec,
                btnAyril);
            if (changed)
            {
                await SearchAsync(_pageIndex);
            }

            return ran;
        }

        /// <summary>
        /// Leaves the selected course after a confirmation; ignored while a previous join or leave is
        /// still running.
        /// </summary>
        internal async Task<bool> LeaveAsync()
        {
            var changed = false;
            var ran = await this.RunExclusiveAsync(
                _write,
                async () =>
                {
                    if (lstKurslar.SelectedItem is not CourseItem item)
                    {
                        Prompts.Warn("Lütfen bir ders seçiniz.");
                        return;
                    }

                    if (!Prompts.Confirm(LeaveQuestion(item.Course.Name)))
                    {
                        return;
                    }

                    await _enrollments.LeaveAsync(_session, item.Course.Id);
                    changed = true;
                    Prompts.Inform("Dersten ayrıldınız.");
                },
                btnDersSec,
                btnAyril);
            if (changed)
            {
                await SearchAsync(_pageIndex);
            }

            return ran;
        }

        private void btnKursDetay_Click(object? sender, EventArgs e)
        {
            if (lstKurslar.SelectedItem is not CourseItem item)
            {
                Prompts.Warn("Lütfen bir ders seçin.");
                return;
            }

            using var details = _forms.Create<KursDetayForm>(_session, item.Course.Id);
            details.ShowDialog(this);
        }

        private async void btnDersSec_Click(object? sender, EventArgs e) => await EnrollAsync();

        private async void btnAyril_Click(object? sender, EventArgs e) => await LeaveAsync();

        private async void btnAra_Click(object? sender, EventArgs e) => await SearchAsync(0);

        private async void btnOnceki_Click(object? sender, EventArgs e) => await SearchAsync(Math.Max(0, _pageIndex - 1));

        private async void btnSonraki_Click(object? sender, EventArgs e) => await SearchAsync(_pageIndex + 1);

        private async void Filter_Changed(object? sender, EventArgs e)
        {
            if (_ready)
            {
                await SearchAsync(0);
            }
        }

        private async void btnTemizle_Click(object? sender, EventArgs e)
        {
            txtAra.Clear();
            txtKonu.Clear();
            txtOgretmen.Clear();
            chkBosKontenjan.Checked = false;
            cmbKayit.SelectedIndex = 0;
            await SearchAsync(0);
        }

        private void btnKurslarim_Click(object? sender, EventArgs e)
        {
            using var mine = _forms.Create<KursNotlariForm>(_session);
            mine.ShowDialog(this);
        }
    }
}
