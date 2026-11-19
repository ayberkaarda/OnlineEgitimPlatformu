using OnlineEgitim.Business.Services;
using OnlineEgitim.UI.Infrastructure;
using OnlineEgitim.UI.Theming;

namespace OnlineEgitim.UI
{
    /// <summary>"Kurslarım": the courses the signed-in student joined.</summary>
    public sealed class KursNotlariForm : AppDialog
    {
        private readonly IEnrollmentService _enrollments;
        private readonly UserSession _session;

        private readonly ThemedListBox lstKurslar = new() { Name = nameof(lstKurslar), AccessibleName = "Kurslarım" };

        public KursNotlariForm(IEnrollmentService enrollments, UserSession session)
            : base("Kurslarım", "Kayıtlı olduğunuz kurslar ve öğretmenleri.", Glyphs.Library, new Size(600, 500), new Size(480, 420), modal: true)
        {
            ArgumentNullException.ThrowIfNull(enrollments);
            ArgumentNullException.ThrowIfNull(session);

            _enrollments = enrollments;
            _session = session;
            BuildLayout();
            Load += async (_, _) => await LoadAsync();
        }

        internal async Task LoadAsync()
        {
            lstKurslar.IsLoading = true;
            try
            {
                await this.RunAsync(async () =>
                {
                    var courses = await _enrollments.GetMyCoursesAsync(_session);
                    lstKurslar.ReplaceItems(courses.Select(c => new CourseItem(c, CourseItemStyle.Mine)));
                });
            }
            finally
            {
                lstKurslar.IsLoading = false;
            }
        }

        private void BuildLayout()
        {
            Name = nameof(KursNotlariForm);
            lstKurslar.Describe = item => item is CourseItem course ? course.Row() : new ListRow(item.ToString() ?? string.Empty);
            lstKurslar.EmptyText = "Henüz bir kursa kayıtlı değilsiniz. Kurs kataloğundan bir kurs seçip kaydolabilirsiniz.";
            var card = FormLayout.Card("myCoursesCard", null, lstKurslar);
            card.Margin = Padding.Empty;
            Content.Controls.Add(card);

            SetFooterNote("Bir kursun notları, katalogda Kurs detayı ile açılır.");
            SetFooterButtons(CreateCloseButton("btnGeri"));
            FinishLayout();
        }
    }
}
