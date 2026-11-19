using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// Creates a course for the signed-in teacher. The teacher fields show who the course belongs to;
    /// they cannot be changed, so no course is created for, and no account created as, another teacher.
    /// </summary>
    public partial class KursEklemeForm : Form
    {
        private readonly ICourseService _courses;
        private readonly UserSession _session;
        private readonly ExclusiveOperation _add = new();

        public KursEklemeForm(ICourseService courses, UserSession session)
        {
            ArgumentNullException.ThrowIfNull(courses);
            ArgumentNullException.ThrowIfNull(session);

            _courses = courses;
            _session = session;
            Prompts = new MessageBoxPrompts(this);

            InitializeComponent();
            _add.HoldClosingOf(this);
            Icon = AppAssets.Icon;
            Text = AppText.Title + " - " + Text;
            txtKursAd.MaxLength = InputRules.CourseNameMaxLength;
            txtKonu.MaxLength = InputRules.TopicMaxLength;
            txtOgretici.ReadOnly = true;
            txtOgretici.TabStop = false;
            txtUzmanlik.ReadOnly = true;
            txtUzmanlik.TabStop = false;
            txtOgretici.Text = session.DisplayName;
            AcceptButton = btnEkle;
            Load += async (_, _) => await LoadAsync();
        }

        /// <summary>Information dialogs; replaced in tests so no window opens.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal IUserPrompts Prompts { get; set; }

        /// <summary>Number of courses created while the form was open.</summary>
        internal int CoursesAdded { get; private set; }

        /// <summary>Shows the signed-in teacher's name and specialty.</summary>
        internal Task LoadAsync() =>
            this.RunAsync(async () =>
            {
                var profile = await _courses.GetMyProfileAsync(_session);
                txtOgretici.Text = profile.FullName;
                txtUzmanlik.Text = profile.Specialty;
            });

        /// <summary>Creates the course; ignored while a previous one is still being saved.</summary>
        internal Task<bool> AddAsync() =>
            this.RunExclusiveAsync(
                _add,
                async () =>
                {
                    await _courses.CreateAsync(_session, txtKursAd.Text, txtKonu.Text);
                    CoursesAdded++;
                    Prompts.Inform("Kurs başarıyla eklendi!");
                    txtKursAd.Clear();
                    txtKonu.Clear();
                    _add.FocusAfterUnlock(txtKursAd);
                },
                btnEkle);

        private async void btnEkle_Click(object? sender, EventArgs e) => await AddAsync();
    }
}
