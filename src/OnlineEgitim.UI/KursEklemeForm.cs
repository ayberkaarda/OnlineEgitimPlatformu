using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Theming;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// Creates a course for the signed-in teacher, or changes one of the teacher's courses after
    /// <see cref="BeginEdit"/>. The teacher fields show who the course belongs to; they cannot be
    /// changed, so no course is created for, and no account created as, another teacher.
    /// </summary>
    public sealed class KursEklemeForm : AppDialog
    {
        private readonly ICourseService _courses;
        private readonly UserSession _session;
        private readonly ExclusiveOperation _add = new();
        private CourseSummary? _editing;

        private readonly TextBox txtKursAd = new() { Name = nameof(txtKursAd), MaxLength = InputRules.CourseNameMaxLength };
        private readonly TextBox txtKonu = new() { Name = nameof(txtKonu), MaxLength = InputRules.TopicMaxLength };
        private readonly NumericUpDown nudKontenjan = new() { Name = nameof(nudKontenjan), Minimum = 0, Maximum = InputRules.CourseCapacityMax };
        private readonly ThemedCheckBox chkSinirsiz = new() { Name = nameof(chkSinirsiz), Text = "&Sınırsız" };
        private readonly TextBox txtOgretici = new() { Name = nameof(txtOgretici), ReadOnly = true, TabStop = false };
        private readonly TextBox txtUzmanlik = new() { Name = nameof(txtUzmanlik), ReadOnly = true, TabStop = false };
        private readonly ThemedButton btnEkle = FormLayout.Button(nameof(btnEkle), "Kurs ekle", ButtonKind.Primary);

        public KursEklemeForm(ICourseService courses, UserSession session)
            : base("Yeni kurs", "Kursun adını, konusunu ve kontenjanını girin; kurs sizin adınıza açılır.", Glyphs.Add, new Size(600, 520), new Size(500, 460), modal: true)
        {
            ArgumentNullException.ThrowIfNull(courses);
            ArgumentNullException.ThrowIfNull(session);

            _courses = courses;
            _session = session;
            BuildLayout();
            _add.HoldClosingOf(this);
            Capacity = null;
            txtOgretici.Text = session.DisplayName;

            Load += async (_, _) => await LoadAsync();
            btnEkle.Click += async (_, _) => await AddAsync();
            chkSinirsiz.CheckedChanged += (_, _) => nudKontenjan.Enabled = !chkSinirsiz.Checked;
        }

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
            SetTitle("Kursu Düzenle", "Kursun adını, konusunu ya da kontenjanını değiştirin.", Glyphs.Edit);
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

        private void BuildLayout()
        {
            Name = nameof(KursEklemeForm);
            var name = FormLayout.Field("&Kurs adı", FormLayout.Input(txtKursAd));
            var topic = FormLayout.Field("K&onu", FormLayout.Input(txtKonu));
            name.Margin = topic.Margin = new Padding(0, 0, 0, Spacing.M);

            var capacityField = FormLayout.Field("Kon&tenjan", FormLayout.Number(nudKontenjan), "Sıfır, kursu yeni öğrencilere kapatır.");
            var capacity = FormLayout.FieldRow("capacityRow", (capacityField, 0), (FormLayout.FieldAction(chkSinirsiz), 100));

            // Centered on the number box: its top margin plus half the difference of the two heights.
            chkSinirsiz.Margin = new Padding(0, Spacing.XS + ((Sizes.ControlHeight - 22) / 2), 0, 0);
            capacityField.Controls[1].Width = 160;
            capacityField.Controls[1].Anchor = AnchorStyles.Left | AnchorStyles.Top;

            var owner = FormLayout.FieldRow(
                "ownerRow",
                (FormLayout.Field("Öğretmen", FormLayout.Input(txtOgretici)), 50),
                (FormLayout.Field("Uzmanlık alanı", FormLayout.Input(txtUzmanlik)), 50));
            owner.Margin = new Padding(0, Spacing.S, 0, 0);

            var card = FormLayout.Card("courseCard", null, FormLayout.Stack("courseForm", name, topic, capacity, owner), fill: false);
            card.Margin = Padding.Empty;
            Content.Controls.Add(card);

            AcceptButton = btnEkle;
            SetFooterNote("Kurs her zaman sizin adınıza açılır.");
            SetFooterButtons(btnEkle, CreateCloseButton("btnKapat"));
            FinishLayout();
        }
    }
}
