using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// Creates a course for the signed-in teacher, or changes one of the teacher's courses after
    /// <see cref="BeginEdit"/>. The teacher fields show who the course belongs to; they cannot be
    /// changed, so no course is created for, and no account created as, another teacher.
    /// </summary>
    public partial class KursEklemeForm : Form
    {
        private readonly ICourseService _courses;
        private readonly UserSession _session;
        private readonly ExclusiveOperation _add = new();
        private CourseSummary? _editing;

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
            nudKontenjan.Minimum = 0;
            nudKontenjan.Maximum = InputRules.CourseCapacityMax;
            Capacity = null;
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

        /// <summary>Whether the course being edited was saved.</summary>
        internal bool Saved { get; private set; }

        /// <summary>The capacity as entered: <see langword="null"/> when "Sınırsız" is ticked.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal int? Capacity
        {
            get => chkSinirsiz.Checked ? null : (int)nudKontenjan.Value;
            set
            {
                chkSinirsiz.Checked = value is null;
                nudKontenjan.Value = value ?? 0;
                nudKontenjan.Enabled = value is not null;
            }
        }

        /// <summary>Turns the form into the editor of <paramref name="course"/>, one of the teacher's own courses.</summary>
        internal void BeginEdit(CourseSummary course)
        {
            ArgumentNullException.ThrowIfNull(course);

            _editing = course;
            Text = AppText.Title + " - Kursu Düzenle";
            btnEkle.Text = "Kaydet";
            txtKursAd.Text = course.Name;
            txtKonu.Text = course.Topic ?? string.Empty;

            // The database only requires a capacity of at least 0; one set outside the application can be
            // larger than the field allows. It is shown at the limit, with a warning, instead of failing.
            if (course.Capacity is { } stored && (stored > InputRules.CourseCapacityMax || stored < 0))
            {
                Capacity = Math.Clamp(stored, 0, InputRules.CourseCapacityMax);
                Prompts.Warn(CapacityOutOfRange(stored));
                return;
            }

            Capacity = course.Capacity;
        }

        /// <summary>The warning shown when the stored capacity lies outside what the form accepts.</summary>
        internal static string CapacityOutOfRange(int stored) => string.Create(
            System.Globalization.CultureInfo.CurrentCulture,
            $"Bu kursun kayıtlı kontenjanı ({stored}) uygulamanın sınırının ({InputRules.CourseCapacityMax}) dışında. Alan {Math.Clamp(stored, 0, InputRules.CourseCapacityMax)} olarak gösteriliyor; kaydederseniz kontenjan bu değer olur.");

        /// <summary>Shows the signed-in teacher's name and specialty.</summary>
        internal Task LoadAsync() =>
            this.RunAsync(async () =>
            {
                var profile = await _courses.GetMyProfileAsync(_session);
                txtOgretici.Text = profile.FullName;
                txtUzmanlik.Text = profile.Specialty;
            });

        /// <summary>Creates or saves the course; ignored while a previous one is still being saved.</summary>
        internal Task<bool> AddAsync() =>
            this.RunExclusiveAsync(
                _add,
                async () =>
                {
                    if (_editing is { } course)
                    {
                        await _courses.UpdateAsync(_session, course.Id, txtKursAd.Text, txtKonu.Text, Capacity);
                        Saved = true;
                        Prompts.Inform("Kurs kaydedildi.");
                        DialogResult = DialogResult.OK;
                        return;
                    }

                    await _courses.CreateAsync(_session, txtKursAd.Text, txtKonu.Text, Capacity);
                    CoursesAdded++;
                    Prompts.Inform("Kurs başarıyla eklendi!");
                    txtKursAd.Clear();
                    txtKonu.Clear();
                    Capacity = null;
                    _add.FocusAfterUnlock(txtKursAd);
                },
                btnEkle);

        private async void btnEkle_Click(object? sender, EventArgs e) => await AddAsync();

        private void chkSinirsiz_CheckedChanged(object? sender, EventArgs e) => nudKontenjan.Enabled = !chkSinirsiz.Checked;
    }
}
