using System.Globalization;
using OnlineEgitim.Business.Services;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// Details of a course: name, subject, teacher, notes and students. A course's teacher sees the
    /// students' names; a student sees how many joined and the notes of the courses they joined.
    /// </summary>
    public partial class KursDetayForm : Form
    {
        internal const string NotesHidden = "Notları görmek için bu derse kayıtlı olmalısınız.";

        private readonly ICourseService _courses;
        private readonly UserSession _session;
        private readonly int _courseId;

        public KursDetayForm(ICourseService courses, UserSession session, int courseId)
        {
            ArgumentNullException.ThrowIfNull(courses);
            ArgumentNullException.ThrowIfNull(session);

            _courses = courses;
            _session = session;
            _courseId = courseId;

            InitializeComponent();
            Icon = AppAssets.Icon;
            Text = AppText.Title + " - " + Text;
            label2.Text = "Konu";
            Load += async (_, _) =>
            {
                if (!await LoadAsync())
                {
                    Close();
                }
            };
        }

        /// <summary>
        /// Shows the course. Returns <see langword="false"/> when it could not be shown (the reason has
        /// been reported), as the original form closed itself then.
        /// </summary>
        internal async Task<bool> LoadAsync()
        {
            CourseDetails? details = null;
            await this.RunAsync(async () => details = await _courses.GetDetailsAsync(_session, _courseId));
            if (details is null)
            {
                return false;
            }

            lblKursAdi.Text = details.Name;
            lblUzmanlik.Text = string.IsNullOrWhiteSpace(details.Topic) ? "-" : details.Topic;
            lblEgitmen.Text = details.TeacherName;

            if (details.NotesVisible)
            {
                lstbNotlar.ReplaceItems(details.Notes);
            }
            else
            {
                lstbNotlar.ReplaceItems([NotesHidden]);
            }

            if (details.Students is { } students)
            {
                label4.Text = "Kayıtlı Öğrenciler:";
                lstOgrenciler.Visible = true;
                lstOgrenciler.ReplaceItems(students);
            }
            else
            {
                // Students see how many joined, not who.
                label4.Text = details.Capacity is { } capacity
                    ? string.Create(CultureInfo.InvariantCulture, $"Kayıtlı Öğrenci Sayısı: {details.StudentCount} / {capacity}")
                    : string.Create(CultureInfo.InvariantCulture, $"Kayıtlı Öğrenci Sayısı: {details.StudentCount}");
                lstOgrenciler.Visible = false;
                lstOgrenciler.Items.Clear();
            }

            return true;
        }

        private void btnKapat_Click(object? sender, EventArgs e) => Close();
    }
}
