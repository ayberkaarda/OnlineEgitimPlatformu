using System.Globalization;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// Teacher panel: the teacher's own courses, the students of the selected course, adding notes,
    /// creating, editing and deleting courses.
    /// </summary>
    public partial class OgretmenForm : Form, ISessionPanel
    {
        private readonly ICourseService _courses;
        private readonly IEnrollmentService _enrollments;
        private readonly IFormFactory _forms;
        private readonly UserSession _session;
        private readonly ExclusiveOperation _addNote = new();
        private readonly ExclusiveOperation _deleteCourse = new();
        private readonly RequestSequence _studentRequests = new();
        private bool _binding;
        private bool _signedOut;

        public OgretmenForm(ICourseService courses, IEnrollmentService enrollments, IFormFactory forms, UserSession session)
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
            pictureBox1.Image = AppAssets.Logo;
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            txtNotEkle.MaxLength = InputRules.NoteMaxLength;
            Load += async (_, _) => await LoadAsync();
        }

        bool ISessionPanel.SignedOut => _signedOut;

        /// <summary>Whether the session ended and the sign-in has to be shown again.</summary>
        internal bool SignedOut => _signedOut;

        void ISessionPanel.EndSession()
        {
            _signedOut = true;
            SessionEnd.Close(this);
        }

        /// <summary>Information dialogs; replaced in tests so no window opens.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal IUserPrompts Prompts { get; set; }

        /// <summary>
        /// Lists the teacher's courses. Rebinding the list is not a selection by the user: no student
        /// list is requested for it, and the previous selection and its students are cleared.
        /// </summary>
        internal Task LoadAsync() =>
            this.RunAsync(async () =>
            {
                var courses = await _courses.GetTeachingAsync(_session);
                _binding = true;
                try
                {
                    lstKurslar.ReplaceItems(courses.Select(c => new CourseItem(c, CourseItemStyle.Teaching)));
                    lstKurslar.ClearSelected();
                }
                finally
                {
                    _binding = false;
                }

                _studentRequests.Next(); // answers to earlier selections are stale now
                lstKayitliOgrenciler.Items.Clear();
            });

        /// <summary>
        /// Lists the students of the selected course. Selections can change faster than the answers
        /// arrive, so only the answer to the latest request is shown.
        /// </summary>
        internal async Task LoadStudentsAsync()
        {
            if (_binding)
            {
                return;
            }

            var request = _studentRequests.Next();
            lstKayitliOgrenciler.Items.Clear();
            if (lstKurslar.SelectedItem is not CourseItem selected)
            {
                return;
            }

            await this.RunAsync(async () =>
            {
                var students = await _enrollments.GetStudentsAsync(_session, selected.Course.Id);
                if (_studentRequests.IsLatest(request) && !IsDisposed)
                {
                    lstKayitliOgrenciler.ReplaceItems(students);
                }
            });
        }

        /// <summary>Adds the note to the selected course; ignored while a previous one is still being saved.</summary>
        internal Task<bool> AddNoteAsync() =>
            this.RunExclusiveAsync(
                _addNote,
                async () =>
                {
                    var courseId = lstKurslar.SelectedItem is CourseItem item ? item.Course.Id : 0;
                    await _courses.AddNoteAsync(_session, courseId, txtNotEkle.Text);
                    Prompts.Inform("Not başarıyla eklendi!");
                    txtNotEkle.Clear();
                    _addNote.FocusAfterUnlock(txtNotEkle);
                },
                btnNotEkle);

        /// <summary>
        /// Deletes the selected course after a confirmation that names the number of its students; the
        /// service deletes only while the course still has that many. Ignored while a deletion is pending.
        /// </summary>
        internal async Task<bool> DeleteCourseAsync()
        {
            var deleted = false;
            var ran = await this.RunExclusiveAsync(
                _deleteCourse,
                async () =>
                {
                    if (lstKurslar.SelectedItem is not CourseItem item)
                    {
                        Prompts.Warn("Lütfen bir kurs seçin.");
                        return;
                    }

                    var details = await _courses.GetDetailsAsync(_session, item.Course.Id);
                    var question = details.StudentCount > 0
                        ? string.Create(
                            CultureInfo.CurrentCulture,
                            $"\"{details.Name}\" kursunda {details.StudentCount} kayıtlı öğrenci var. Kurs silinirse kayıtları kapanır ve öğrenciler kursun notlarını göremez. Kurs silinsin mi?")
                        : $"\"{details.Name}\" kursu silinsin mi?";
                    if (!Prompts.Confirm(question))
                    {
                        return;
                    }

                    await _courses.DeleteAsync(_session, item.Course.Id, details.StudentCount);
                    deleted = true;
                    Prompts.Inform("Kurs silindi.");
                },
                btnSil,
                btnDuzenle,
                btnYeniKurs);
            if (deleted)
            {
                await LoadAsync();
            }

            return ran;
        }

        private async void lstKurslar_SelectedIndexChanged(object? sender, EventArgs e) => await LoadStudentsAsync();

        private async void btnSil_Click(object? sender, EventArgs e) => await DeleteCourseAsync();

        private async void btnDuzenle_Click(object? sender, EventArgs e)
        {
            if (_deleteCourse.IsRunning)
            {
                return;
            }

            if (lstKurslar.SelectedItem is not CourseItem item)
            {
                Prompts.Warn("Lütfen bir kurs seçin.");
                return;
            }

            bool saved;
            using (var form = _forms.Create<KursEklemeForm>(_session))
            {
                form.BeginEdit(item.Course);
                form.ShowDialog(this);
                saved = form.Saved;
            }

            if (saved)
            {
                await LoadAsync();
            }
        }

        private async void btnNotEkle_Click(object? sender, EventArgs e) => await AddNoteAsync();

        private void btnKurtarmaKodu_Click(object? sender, EventArgs e)
        {
            using var form = _forms.Create<KurtarmaKoduForm>(_session);
            form.ShowDialog(this);
        }

        private async void btnYeniKurs_Click(object? sender, EventArgs e)
        {
            int added;
            using (var form = _forms.Create<KursEklemeForm>(_session))
            {
                form.ShowDialog(this);
                added = form.CoursesAdded;
            }

            if (added > 0)
            {
                await LoadAsync();
            }
        }
        private void btnSifre_Click(object? sender, EventArgs e)
        {
            if (PasswordChange.Show(this, _forms, _session))
            {
                ((ISessionPanel)this).EndSession();
            }
        }
    }
}
