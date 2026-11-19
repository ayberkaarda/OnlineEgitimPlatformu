using System.Globalization;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Theming;

namespace OnlineEgitim.UI
{
    /// <summary>
    /// Student panel: the course catalogue with search, filters and pages, course details, joining and
    /// leaving a course and the student's own courses.
    /// </summary>
    public sealed class MainForm : AppDialog, ISessionPanel
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
        private bool _signedOut;
        private string? _signOutNotice;
        private int _pageIndex;

        private readonly Label lblhosgeldin = new() { Name = nameof(lblhosgeldin), AutoSize = true, UseMnemonic = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly TextBox txtAra = new() { Name = nameof(txtAra), MaxLength = InputRules.CourseNameMaxLength, PlaceholderText = "Örneğin: Python" };
        private readonly TextBox txtKonu = new() { Name = nameof(txtKonu), MaxLength = InputRules.TopicMaxLength };
        private readonly TextBox txtOgretmen = new() { Name = nameof(txtOgretmen), MaxLength = InputRules.PersonNameMaxLength };
        private readonly ThemedComboBox cmbKayit = FormLayout.Picker(nameof(cmbKayit), 200);
        private readonly ThemedCheckBox chkBosKontenjan = new() { Name = nameof(chkBosKontenjan), Text = "Yalnız &boş yeri olanlar" };
        private readonly ThemedButton btnAra = FormLayout.Button(nameof(btnAra), "Ara", ButtonKind.Secondary, Glyphs.Search);
        private readonly ThemedButton btnTemizle = FormLayout.Button(nameof(btnTemizle), "Temizle", ButtonKind.Subtle);
        private readonly ThemedListBox lstKurslar = new() { Name = nameof(lstKurslar), AccessibleName = "Kurs kataloğu" };
        private readonly ThemedButton btnOnceki = FormLayout.Button(nameof(btnOnceki), "‹ Önceki", ButtonKind.Subtle);
        private readonly Label lblSayfa = FormLayout.Hint(string.Empty);
        private readonly ThemedButton btnSonraki = FormLayout.Button(nameof(btnSonraki), "Sonraki ›", ButtonKind.Subtle);
        private readonly ThemedButton btnKursDetay = FormLayout.Button(nameof(btnKursDetay), "Kurs detayı", ButtonKind.Secondary, Glyphs.Details);
        private readonly ThemedButton btnDersSec = FormLayout.Button(nameof(btnDersSec), "Derse kaydol", ButtonKind.Primary);
        private readonly ThemedButton btnAyril = FormLayout.Button(nameof(btnAyril), "Dersten ayrıl", ButtonKind.Danger);
        private readonly ThemedButton btnKurslarim = FormLayout.Button(nameof(btnKurslarim), "Kurslarım", ButtonKind.Secondary, Glyphs.Library);
        private readonly ThemedButton btnSifre = FormLayout.Button(nameof(btnSifre), "Şifremi değiştir", ButtonKind.Secondary, Glyphs.Lock);
        private readonly ThemedButton btnCikis = FormLayout.Button(nameof(btnCikis), "Oturumu kapat", ButtonKind.Secondary);

        public MainForm(ICourseService courses, IEnrollmentService enrollments, IFormFactory forms, UserSession session)
            : base(
                "Öğrenci paneli",
                "Kurs arayın, kaydolun ya da ayrılın; kayıtlı olduğunuz kursların notlarını görün.",
                Glyphs.Library,
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
            lblhosgeldin.Text = $"Hoş geldin, {session.DisplayName}!";
            cmbKayit.Items.AddRange(["Tümü", "Kayıtlı olduklarım", "Kayıtlı olmadıklarım"]);
            cmbKayit.SelectedIndex = 0; // set before _ready: not a choice of the user, no search
            _ready = true;

            Load += async (_, _) => await LoadAsync();
            chkBosKontenjan.CheckedChanged += Filter_Changed;
            cmbKayit.SelectedIndexChanged += Filter_Changed;
            btnAra.Click += async (_, _) => await SearchAsync(0);
            btnTemizle.Click += async (_, _) => await ClearFiltersAsync();
            btnOnceki.Click += async (_, _) => await SearchAsync(Math.Max(0, _pageIndex - 1));
            btnSonraki.Click += async (_, _) => await SearchAsync(_pageIndex + 1);
            btnKursDetay.Click += (_, _) => OpenDetails();
            btnDersSec.Click += async (_, _) => await EnrollAsync();
            btnAyril.Click += async (_, _) => await LeaveAsync();
            lstKurslar.DoubleClick += (_, _) => OpenDetails();
            btnKurslarim.Click += (_, _) => OpenMyCourses();
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
            lstKurslar.IsLoading = true;
            try
            {
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
            finally
            {
                if (_searchRequests.IsLatest(request))
                {
                    lstKurslar.IsLoading = false;
                }
            }
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

        private void OpenDetails()
        {
            if (lstKurslar.SelectedItem is not CourseItem item)
            {
                Prompts.Warn("Lütfen bir ders seçin.");
                return;
            }

            using var details = _forms.Create<KursDetayForm>(_session, item.Course.Id);
            details.ShowDialog(this);
        }

        private async void Filter_Changed(object? sender, EventArgs e)
        {
            if (_ready)
            {
                await SearchAsync(0);
            }
        }

        private async Task ClearFiltersAsync()
        {
            txtAra.Clear();
            txtKonu.Clear();
            txtOgretmen.Clear();
            chkBosKontenjan.Checked = false;
            cmbKayit.SelectedIndex = 0;
            await SearchAsync(0);
        }

        private void OpenMyCourses()
        {
            using var mine = _forms.Create<KursNotlariForm>(_session);
            mine.ShowDialog(this);
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
            Name = nameof(MainForm);
            lblhosgeldin.Font = Theme.Fonts.Body;
            lblhosgeldin.ForeColor = Theme.Current.TextSecondary;
            SetHeaderAside(lblhosgeldin);

            // Search card: the filters and the search actions.
            var name = FormLayout.Field("&Kurs adı", FormLayout.Input(txtAra));
            var topic = FormLayout.Field("K&onu", FormLayout.Input(txtKonu));
            var teacher = FormLayout.Field("Öğ&retmen", FormLayout.Input(txtOgretmen));
            var enrollment = FormLayout.Field("Kayıt &durumu", cmbKayit);
            name.Margin = topic.Margin = teacher.Margin = enrollment.Margin = new Padding(0, 0, 0, Spacing.M);
            chkBosKontenjan.Margin = new Padding(0, 0, 0, Spacing.L);
            btnAra.Margin = new Padding(0, 0, Spacing.S, 0);
            btnTemizle.Margin = Padding.Empty;
            var searchActions = FormLayout.Bar("searchActions", (btnAra, false), (btnTemizle, false), (FormLayout.Spacer(), true));
            var filters = FormLayout.Stack("filterForm", name, topic, teacher, enrollment, chkBosKontenjan, searchActions);
            chkBosKontenjan.Dock = DockStyle.None;
            chkBosKontenjan.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            var searchCard = FormLayout.Card("searchCard", "Kurs ara", filters);
            filters.Dock = DockStyle.Top;
            searchCard.Margin = new Padding(0, 0, Spacing.S, 0);

            // Catalogue card: the list, its pages and the actions on the selected course.
            lstKurslar.Describe = item => item is CourseItem course ? course.Row() : new ListRow(item.ToString() ?? string.Empty);
            lstKurslar.EmptyText = "Aramanıza uyan kurs yok. Filtreleri değiştirin ya da Temizle ile hepsini görün.";
            lblSayfa.Name = nameof(lblSayfa);
            lblSayfa.AutoSize = true;
            lblSayfa.TextAlign = ContentAlignment.MiddleCenter;
            lblSayfa.Margin = new Padding(Spacing.S, 0, Spacing.S, 0);
            btnOnceki.Enabled = false;
            btnSonraki.Enabled = false;
            btnOnceki.Margin = btnSonraki.Margin = Padding.Empty;
            var pager = FormLayout.Bar("pager", (btnOnceki, false), (FormLayout.Spacer("pagerSpacerLeft"), true), (lblSayfa, false), (FormLayout.Spacer("pagerSpacerRight"), true), (btnSonraki, false));
            pager.Margin = new Padding(0, Spacing.S, 0, 0);
            btnKursDetay.Margin = new Padding(0, 0, Spacing.S, 0);
            btnDersSec.Margin = new Padding(0, 0, Spacing.S, 0);
            btnAyril.Margin = Padding.Empty;
            var actions = FormLayout.Bar("courseActions", (btnKursDetay, false), (FormLayout.Spacer(), true), (btnDersSec, false), (btnAyril, false));
            actions.Margin = new Padding(0, Spacing.M, 0, 0);
            var catalogCard = FormLayout.Card("catalogCard", "Kurs kataloğu", FormLayout.Page("catalogPage", (lstKurslar, true), (pager, false), (actions, false)));
            catalogCard.Margin = Padding.Empty;

            Content.Controls.Add(FormLayout.Columns("layout", searchCard, 32, catalogCard));

            AcceptButton = btnAra;
            CancelButton = btnCikis;
            SetFooterNote("Kaydolmak, ayrılmak ya da notları görmek için listeden bir kurs seçin.");
            SetFooterButtons(btnKurslarim, btnSifre, btnCikis);
            FinishLayout();
        }
    }
}
