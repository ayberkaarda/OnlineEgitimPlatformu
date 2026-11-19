using System.Globalization;
using OnlineEgitim.Business.Services;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Theming;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// Details of a course: name, subject, teacher, notes and students. A course's teacher sees the
    /// students' names; a student sees how many joined and the notes of the courses they joined.
    /// </summary>
    public sealed class KursDetayForm : AppDialog
    {
        internal const string NotesHidden = "Notları görmek için bu derse kayıtlı olmalısınız.";

        internal const string NoNotes = "Bu kursa henüz not eklenmemiş.";

        internal const string NamesHidden = "Notları kursun öğretmeni ekler; öğrencilerin adlarını yalnızca o görür.";

        private readonly ICourseService _courses;
        private readonly UserSession _session;
        private readonly int _courseId;

        private readonly Label lblKursAdi = FormLayout.Text(nameof(lblKursAdi));
        private readonly Label lblEgitmen = FormLayout.Text(nameof(lblEgitmen));
        private readonly Label lblUzmanlik = FormLayout.Text(nameof(lblUzmanlik));
        private readonly Label lblOgrenciSayisi = FormLayout.Text(nameof(lblOgrenciSayisi));
        private readonly ThemedListBox lstbNotlar = new() { Name = nameof(lstbNotlar), AccessibleName = "Notlar", WrapText = true };
        private readonly ThemedListBox lstOgrenciler = new() { Name = nameof(lstOgrenciler), AccessibleName = "Kayıtlı öğrenciler", TwoLines = false };

        public KursDetayForm(ICourseService courses, UserSession session, int courseId)
            : base("Kurs detayları", "Kursun öğretmeni, konusu, notları ve kayıtlı öğrencileri.", Glyphs.Details, new Size(720, 560), new Size(600, 480), modal: true)
        {
            ArgumentNullException.ThrowIfNull(courses);
            ArgumentNullException.ThrowIfNull(session);

            _courses = courses;
            _session = session;
            _courseId = courseId;
            BuildLayout();
            Load += async (_, _) =>
            {
                if (!await LoadAsync())
                {
                    Close();
                }
            };
        }

        /// <summary>
        /// Shows the course. Returns <see langword="false"/> when it could not be shown; the reason is reported
        /// in the window that opened this one (or in a message window), as this window closes then.
        /// </summary>
        internal async Task<bool> LoadAsync()
        {
            CourseDetails? details = null;
            lstbNotlar.IsLoading = true;
            IWin32Window reportTo = Owner as IStatusSurface is not null ? Owner! : new DialogOwner(this);
            await this.RunAsync(async () => details = await _courses.GetDetailsAsync(_session, _courseId), reportTo);
            lstbNotlar.IsLoading = false;
            if (details is null)
            {
                return false;
            }

            lblKursAdi.Text = details.Name;
            lblUzmanlik.Text = string.IsNullOrWhiteSpace(details.Topic) ? "-" : details.Topic;
            lblEgitmen.Text = details.TeacherName;
            lblOgrenciSayisi.Text = details.Capacity is { } capacity
                ? string.Create(CultureInfo.CurrentCulture, $"{details.StudentCount} / {capacity} kayıtlı öğrenci")
                : string.Create(CultureInfo.CurrentCulture, $"{details.StudentCount} kayıtlı öğrenci (kontenjan sınırsız)");

            lstbNotlar.EmptyText = details.NotesVisible ? NoNotes : NotesHidden;
            lstbNotlar.ReplaceItems(details.NotesVisible ? details.Notes : []);

            if (details.Students is { } students)
            {
                ShowStudentList(true);
                lstOgrenciler.ReplaceItems(students);
            }
            else
            {
                // Students see how many joined (in the facts above), not who; the notes take the whole width.
                ShowStudentList(false);
                lstOgrenciler.Items.Clear();
            }

            return true;
        }

        private void BuildLayout()
        {
            Name = nameof(KursDetayForm);
            lblKursAdi.Font = Theme.Fonts.Subtitle;
            lblKursAdi.Margin = new Padding(0, 0, 0, Spacing.M);
            var facts = new TableLayoutPanel
            {
                Name = "courseFacts",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 3,
                Dock = DockStyle.Top,
                Margin = Padding.Empty,
            };
            facts.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            facts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddFact(facts, 0, "Öğretmen", lblEgitmen);
            AddFact(facts, 1, "Konu", lblUzmanlik);
            AddFact(facts, 2, "Kontenjan", lblOgrenciSayisi);
            var infoCard = FormLayout.Card("infoCard", null, FormLayout.Stack("infoStack", lblKursAdi, facts), fill: false);

            lstbNotlar.EmptyText = NoNotes;
            var notesCard = FormLayout.Card("notesCard", "Notlar", lstbNotlar);
            notesCard.Margin = Padding.Empty;

            lstOgrenciler.EmptyText = "Bu kursa henüz kayıtlı öğrenci yok.";
            _studentsCard = FormLayout.Card("studentsCard", "Kayıtlı öğrenciler", lstOgrenciler);
            _studentsCard.Margin = Padding.Empty;
            _lists = FormLayout.Columns("lists", notesCard, 60, _studentsCard);

            Content.Controls.Add(FormLayout.Page("detailsPage", (infoCard, false), (_lists, true)));

            SetFooterNote(NamesHidden);
            SetFooterButtons(CreateCloseButton("btnKapat"));
            FinishLayout();
        }

        private CardPanel _studentsCard = null!;
        private TableLayoutPanel _lists = null!;

        /// <summary>Whether the students' names are shown beside the notes; without them the notes take the whole width.</summary>
        internal bool ShowsStudentNames => !FormLayout.IsHidden(_studentsCard);

        private void ShowStudentList(bool shown)
        {
            FormLayout.SetShown(_studentsCard, shown);
            _lists.ColumnStyles[0].Width = shown ? 60 : 100;
            _lists.ColumnStyles[1].Width = shown ? 40 : 0;
            _lists.Controls[0].Margin = shown ? new Padding(0, 0, Spacing.S, 0) : Padding.Empty;
        }

        private static void AddFact(TableLayoutPanel facts, int row, string caption, Label value)
        {
            facts.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var label = FormLayout.Hint(caption);
            label.Margin = new Padding(0, 0, Spacing.L, Spacing.XS);
            value.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            value.Margin = new Padding(0, 0, 0, Spacing.XS);
            facts.Controls.Add(label, 0, row);
            facts.Controls.Add(value, 1, row);
        }
    }
}
