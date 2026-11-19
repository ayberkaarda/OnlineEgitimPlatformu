using OnlineEgitim.Business.Services;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>Student panel: the course list, course details, joining a course and the student's own courses.</summary>
    public partial class MainForm : Form
    {
        private readonly ICourseService _courses;
        private readonly IEnrollmentService _enrollments;
        private readonly IFormFactory _forms;
        private readonly UserSession _session;
        private readonly ExclusiveOperation _enroll = new();

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
            Load += async (_, _) => await LoadAsync();
        }

        /// <summary>Information dialogs; replaced in tests so no window opens.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal IUserPrompts Prompts { get; set; }

        /// <summary>Lists every course.</summary>
        internal Task LoadAsync() =>
            this.RunAsync(async () =>
            {
                var courses = await _courses.GetCatalogAsync(_session);
                lstKurslar.ReplaceItems(courses.Select(c => new CourseItem(c, CourseItemStyle.Catalog)));
            });

        /// <summary>Joins the selected course; ignored while a previous attempt is still running.</summary>
        internal Task<bool> EnrollAsync() =>
            this.RunExclusiveAsync(
                _enroll,
                async () =>
                {
                    if (lstKurslar.SelectedItem is not CourseItem item)
                    {
                        Prompts.Warn("Lütfen bir ders seçiniz.");
                        return;
                    }

                    await _enrollments.EnrollAsync(_session, item.Course.Id);
                    Prompts.Inform("Ders başarıyla seçildi!");
                },
                btnDersSec);

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

        private void btnKurslarim_Click(object? sender, EventArgs e)
        {
            using var mine = _forms.Create<KursNotlariForm>(_session);
            mine.ShowDialog(this);
        }
    }
}
