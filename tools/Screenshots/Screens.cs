using System.Globalization;
using OnlineEgitim.UI;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.Screenshots;

/// <summary>The screens shown in the README, each prepared with synthetic data as a student, teacher or administrator would see it.</summary>
internal static class Screens
{
    /// <summary>File name (kebab-case, without extension) and how to build the screen.</summary>
    public static IReadOnlyList<(string Name, Func<SyntheticSchool, Form> Build)> All { get; } =
    [
        ("ogrenci-girisi", school => new GirisForm(school, new UnusedFormFactory())),
        ("ogrenci-kaydi", school => new KayitForm(school)),
        ("ogretmen-girisi", school => new OgretmenGirisForm(school)),
        ("ilk-kurulum", school => new IlkKurulumForm(school)),
        ("hesap-kurtarma", Recovery),
        ("sifre-degistirme", school => new SifreDegistirForm(school, school.Student)),
        ("ogrenci-paneli", StudentPanel),
        ("kurs-detayi", CourseDetails),
        ("kurslarim", MyCourses),
        ("ogretmen-paneli", TeacherPanel),
        ("kurs-duzenleme", EditCourse),
        ("kurtarma-kodu", RecoveryCode),
        ("yonetici-paneli", AdminPanel),
        ("onay-penceresi", Confirmation),
    ];

    /// <summary>The administrator panel after a reset code was issued for the selected student.</summary>
    private static YoneticiForm AdminPanel(SyntheticSchool school)
    {
        var form = new YoneticiForm(school, new UnusedFormFactory(), school.Admin);
        Run(form.LoadAsync());
        Find<ListBox>(form, "lstHesaplar").SelectedIndex = 6;
        Run(form.IssueResetCodeAsync());
        return form;
    }

    /// <summary>The question asked before a student leaves a course.</summary>
    private static MessageDialog Confirmation(SyntheticSchool school) => new(
        MainForm.LeaveQuestion("Temel Matematik Atölyesi"),
        "Onay",
        MessageBoxButtons.YesNo,
        MessageBoxIcon.Question);

    /// <summary>The teacher panel with a course selected, its students listed and a note being written.</summary>
    private static OgretmenForm TeacherPanel(SyntheticSchool school)
    {
        var form = new OgretmenForm(school, school, new UnusedFormFactory(), school.Teacher);
        Run(form.LoadAsync());
        Find<ListBox>(form, "lstKurslar").SelectedIndex = 0;
        Find<TextBox>(form, "txtNotEkle").Text = "Cuma gününe kadar döngü alıştırmalarını tamamlayın.";
        return form;
    }

    private static KursEklemeForm EditCourse(SyntheticSchool school)
    {
        var form = new KursEklemeForm(school, school.Teacher);
        form.BeginEdit(school.GetTeachingAsync(school.Teacher).Result[0]);
        Run(form.LoadAsync());
        return form;
    }

    /// <summary>A code issued for a student; the code is a visibly made-up sample.</summary>
    private static KurtarmaKoduForm RecoveryCode(SyntheticSchool school)
    {
        var form = new KurtarmaKoduForm(school, school.Teacher);
        Find<TextBox>(form, "txtAd").Text = "Ada Yalçınkaya";
        Run(form.IssueAsync());
        return form;
    }

    /// <summary>
    /// The student panel after joining a course: the catalogue's first page, the joined course selected and the
    /// result shown inline.
    /// </summary>
    private static MainForm StudentPanel(SyntheticSchool school)
    {
        var form = new MainForm(school, school, new UnusedFormFactory(), school.Student);
        Run(form.LoadAsync());
        Find<ListBox>(form, "lstKurslar").SelectedIndex = 3;
        Run(form.EnrollAsync());
        Find<ListBox>(form, "lstKurslar").SelectedIndex = 3;
        return form;
    }

    private static KursDetayForm CourseDetails(SyntheticSchool school)
    {
        var form = new KursDetayForm(school, school.Student, 1);
        Run(form.LoadAsync());
        return form;
    }

    private static KursNotlariForm MyCourses(SyntheticSchool school)
    {
        var form = new KursNotlariForm(school, school.Student);
        Run(form.LoadAsync());
        return form;
    }

    /// <summary>The recovery as a student, before the code is typed.</summary>
    private static HesapKurtarmaForm Recovery(SyntheticSchool school) => new(school, school);

    /// <summary>The in-memory services complete synchronously; anything else is a bug in the tool.</summary>
    internal static void Run(Task task)
    {
        if (!task.IsCompleted)
        {
            throw new InvalidOperationException("A screen did not finish loading synchronously.");
        }

        task.GetAwaiter().GetResult();
    }

    internal static T Find<T>(Control root, string name)
        where T : Control =>
        root.Controls.Find(name, searchAllChildren: true).OfType<T>().SingleOrDefault()
        ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Control '{0}' was not found.", name));

    /// <summary>No other window is opened while a screen is drawn.</summary>
    private sealed class UnusedFormFactory : IFormFactory
    {
        public T Create<T>(params object[] arguments)
            where T : Form => throw new InvalidOperationException("Screens are not opened while capturing.");
    }
}
