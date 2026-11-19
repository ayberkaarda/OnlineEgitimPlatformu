using System.Globalization;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Theming;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// Teacher panel: the teacher's own courses, the students of the selected course, adding notes,
    /// creating, editing and deleting courses.
    /// </summary>
    public sealed class OgretmenForm : AppDialog, ISessionPanel
    {
        internal const string ChooseACourse = "Öğrencilerini görmek için soldan bir kurs seçin.";

        internal const string NoStudents = "Bu kursa henüz kayıtlı öğrenci yok.";

        private readonly ICourseService _courses;
        private readonly IEnrollmentService _enrollments;
        private readonly IFormFactory _forms;
        private readonly UserSession _session;
        private readonly ExclusiveOperation _addNote = new();
        private readonly ExclusiveOperation _deleteCourse = new();
        private readonly RequestSequence _studentRequests = new();
        private bool _binding;
        private bool _signedOut;
        private string? _signOutNotice;

        private readonly Label lblOgretmen = new() { Name = nameof(lblOgretmen), AutoSize = true, UseMnemonic = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly ThemedListBox lstKurslar = new() { Name = nameof(lstKurslar), AccessibleName = "Kurslarım" };
        private readonly ThemedButton btnYeniKurs = FormLayout.Button(nameof(btnYeniKurs), "Yeni kurs", ButtonKind.Primary, Glyphs.Add);
        private readonly ThemedButton btnDuzenle = FormLayout.Button(nameof(btnDuzenle), "Düzenle", ButtonKind.Secondary, Glyphs.Edit);
        private readonly ThemedButton btnSil = FormLayout.Button(nameof(btnSil), "Sil", ButtonKind.Danger, Glyphs.Delete);
        private readonly ThemedListBox lstKayitliOgrenciler = new() { Name = nameof(lstKayitliOgrenciler), AccessibleName = "Kayıtlı öğrenciler", TwoLines = false };
        private readonly TextBox txtNotEkle = new() { Name = nameof(txtNotEkle), Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, MaxLength = InputRules.NoteMaxLength };
        private readonly ThemedButton btnNotEkle = FormLayout.Button(nameof(btnNotEkle), "Not ekle", ButtonKind.Secondary, Glyphs.Note);
        private readonly ThemedButton btnKurtarmaKodu = FormLayout.Button(nameof(btnKurtarmaKodu), "Kurtarma kodu üret", ButtonKind.Secondary, Glyphs.Key);
        private readonly ThemedButton btnSifre = FormLayout.Button(nameof(btnSifre), "Şifremi değiştir", ButtonKind.Secondary, Glyphs.Lock);
        private readonly ThemedButton btnCikis = FormLayout.Button(nameof(btnCikis), "Oturumu kapat", ButtonKind.Secondary);

        public OgretmenForm(ICourseService courses, IEnrollmentService enrollments, IFormFactory forms, UserSession session)
            : base(
                "Öğretmen paneli",
                "Kurslarınızı yönetin, not ekleyin ve öğrencileriniz için kurtarma kodu üretin.",
                Glyphs.Note,
                new Size(1040, 640),
                new Size(900, 560))
        {
            ArgumentNullException.ThrowIfNull(courses);
            ArgumentNullException.ThrowIfNull(enrollments);
            ArgumentNullException.ThrowIfNull(forms);
            ArgumentNullException.ThrowIfNull(session);

            _courses = courses;
            _enrollments = enrollments;
            _forms = forms;
            _session = session;
            BuildLayout();
            lblOgretmen.Text = session.DisplayName + Environment.NewLine + "Öğretmen hesabı";

            Load += async (_, _) => await LoadAsync();
            lstKurslar.SelectedIndexChanged += async (_, _) => await LoadStudentsAsync();
            btnSil.Click += async (_, _) => await DeleteCourseAsync();
            btnDuzenle.Click += async (_, _) => await EditSelectedAsync();
            btnYeniKurs.Click += async (_, _) => await AddCoursesAsync();
            btnNotEkle.Click += async (_, _) => await AddNoteAsync();
            btnKurtarmaKodu.Click += (_, _) => OpenRecoveryCode();
            btnSifre.Click += (_, _) => ChangePassword();
            btnCikis.Click += (_, _) => ((ISessionPanel)this).EndSession();
        }

        bool ISessionPanel.SignedOut => _signedOut;

        string? ISessionPanel.SignOutNotice => _signOutNotice;

        /// <summary>Whether the session ended and the sign-in has to be shown again.</summary>
        internal bool SignedOut => _signedOut;

        void ISessionPanel.EndSession()
        {
            _signedOut = true;
            SessionEnd.Close(this);
        }

        /// <summary>
        /// Lists the teacher's courses. Rebinding the list is not a selection by the user: no student
        /// list is requested for it, and the previous selection and its students are cleared.
        /// </summary>
        internal async Task LoadAsync()
        {
            lstKurslar.IsLoading = true;
            try
            {
                await this.RunAsync(async () =>
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
                    lstKayitliOgrenciler.IsLoading = false;
                    lstKayitliOgrenciler.EmptyText = ChooseACourse;
                });
            }
            finally
            {
                lstKurslar.IsLoading = false;
            }
        }

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
                lstKayitliOgrenciler.IsLoading = false;
                lstKayitliOgrenciler.EmptyText = ChooseACourse;
                return;
            }

            lstKayitliOgrenciler.EmptyText = NoStudents;
            lstKayitliOgrenciler.IsLoading = true;
            try
            {
                await this.RunAsync(async () =>
                {
                    var students = await _enrollments.GetStudentsAsync(_session, selected.Course.Id);
                    if (_studentRequests.IsLatest(request) && !IsDisposed)
                    {
                        lstKayitliOgrenciler.ReplaceItems(students);
                    }
                });
            }
            finally
            {
                if (_studentRequests.IsLatest(request))
                {
                    lstKayitliOgrenciler.IsLoading = false;
                }
            }
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

        private async Task EditSelectedAsync()
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
                Prompts.Inform("Kurs kaydedildi.");
            }
        }

        private async Task AddCoursesAsync()
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

        private void OpenRecoveryCode()
        {
            using var form = _forms.Create<KurtarmaKoduForm>(_session);
            form.ShowDialog(this);
        }

        private void ChangePassword()
        {
            if (PasswordChange.Show(this, _forms, _session))
            {
                _signOutNotice = SifreDegistirForm.Done;
                ((ISessionPanel)this).EndSession();
            }
        }

        private void BuildLayout()
        {
            Name = nameof(OgretmenForm);
            lblOgretmen.Font = Theme.Fonts.Body;
            lblOgretmen.ForeColor = Theme.Current.TextSecondary;
            SetHeaderAside(lblOgretmen);

            // The teacher's courses with the actions on them.
            lstKurslar.Describe = item => item is CourseItem course ? course.Row() : new ListRow(item.ToString() ?? string.Empty);
            lstKurslar.EmptyText = "Henüz bir kursunuz yok. Yeni kurs ile ilk kursunuzu açın.";
            btnYeniKurs.Margin = btnDuzenle.Margin = new Padding(0, 0, Spacing.S, 0);
            btnSil.Margin = Padding.Empty;
            var courseActions = FormLayout.Bar("courseActions", (btnYeniKurs, false), (btnDuzenle, false), (FormLayout.Spacer(), true), (btnSil, false));
            courseActions.Margin = new Padding(0, Spacing.M, 0, 0);
            var coursesCard = FormLayout.Card("coursesCard", "Kurslarım", FormLayout.Page("coursesPage", (lstKurslar, true), (courseActions, false)));
            coursesCard.Margin = new Padding(0, 0, Spacing.S, 0);

            // The students of the selected course.
            lstKayitliOgrenciler.EmptyText = ChooseACourse;
            var studentsCard = FormLayout.Card("studentsCard", "Kayıtlı öğrenciler", lstKayitliOgrenciler);

            // A note to the selected course.
            var noteField = FormLayout.Field("Seçili kursa &not", FormLayout.Input(txtNotEkle, Sizes.NoteHeight));
            noteField.Margin = new Padding(0, 0, 0, Spacing.S);
            var noteHint = FormLayout.Hint("Kursun öğrencileri notu kurs detayında görür.");
            noteHint.Name = "noteHint";
            noteHint.Margin = new Padding(Spacing.M, 0, 0, 0);
            btnNotEkle.Margin = Padding.Empty;
            var noteActions = FormLayout.Bar("noteActions", (btnNotEkle, false), (noteHint, true));
            var noteCard = FormLayout.Card("noteCard", null, FormLayout.Stack("noteForm", noteField, noteActions), fill: false);

            // Recovery codes for the students of the teacher's courses.
            var codeHint = FormLayout.Hint("Şifresi olmayan ya da şifresini unutan öğrenciniz için tek kullanımlık kod.");
            codeHint.Name = "codeHint";
            codeHint.Margin = new Padding(Spacing.M, 0, 0, 0);
            btnKurtarmaKodu.Margin = Padding.Empty;
            var codeCard = FormLayout.Card("codeCard", null, FormLayout.Bar("codeActions", (btnKurtarmaKodu, false), (codeHint, true)), fill: false);
            codeCard.Margin = Padding.Empty;

            var right = FormLayout.Page("rightColumn", (studentsCard, true), (noteCard, false), (codeCard, false));
            Content.Controls.Add(FormLayout.Columns("layout", coursesCard, 55, right));

            CancelButton = btnCikis;
            SetFooterNote("Düzenlemek, silmek ya da not eklemek için soldan bir kurs seçin.");
            SetFooterButtons(btnSifre, btnCikis);
            FinishLayout();
        }
    }
}
