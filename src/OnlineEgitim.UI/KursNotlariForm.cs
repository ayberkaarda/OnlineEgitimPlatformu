using OnlineEgitim.Business.Services;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>"Kurslarım": the courses the signed-in student joined.</summary>
    public partial class KursNotlariForm : Form
    {
        private readonly IEnrollmentService _enrollments;
        private readonly UserSession _session;

        public KursNotlariForm(IEnrollmentService enrollments, UserSession session)
        {
            ArgumentNullException.ThrowIfNull(enrollments);
            ArgumentNullException.ThrowIfNull(session);

            _enrollments = enrollments;
            _session = session;

            InitializeComponent();
            Icon = AppAssets.Icon;
            Text = AppText.Title + " - " + Text;
            lblKursAdi.Text = "Kurslarım";
            Load += async (_, _) => await LoadAsync();
        }

        internal Task LoadAsync() =>
            this.RunAsync(async () =>
            {
                var courses = await _enrollments.GetMyCoursesAsync(_session);
                lstKurslar.ReplaceItems(courses.Select(c => new CourseItem(c, CourseItemStyle.Mine)));
            });

        private void btnGeri_Click(object? sender, EventArgs e) => Close();
    }
}
