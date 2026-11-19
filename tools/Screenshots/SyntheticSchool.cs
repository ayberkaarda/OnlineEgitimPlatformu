using System.Reflection;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Screenshots;

/// <summary>
/// An invented school in memory: made-up people, e-mail addresses on the reserved example.com domain and
/// courses, so the screenshots never show real or personal data and no database is needed. Every call
/// completes synchronously. Codes are visibly sample values ("ORNEK-…"), never real ones.
/// </summary>
internal sealed class SyntheticSchool : IAuthService, ICourseService, IEnrollmentService, IRecoveryService, ISetupService, IAdministrationService
{
    /// <summary>A visibly made-up single use code for the screens that show one.</summary>
    public const string SampleCode = "ORNEK-12345";

    private static readonly DateTime SampleExpiry = new(2026, 11, 20, 9, 30, 0, DateTimeKind.Utc);

    private readonly List<CatalogEntry> _catalog =
    [
        Entry(1, "Python ile Programlamaya Giriş", 11, "Barış Tuna", "Değişkenler, döngüler ve fonksiyonlar", 30, 24, enrolled: true),
        Entry(2, "Veri Görselleştirme", 11, "Barış Tuna", "Grafik türleri ve renk seçimi", 20, 20),
        Entry(3, "Arayüz Tasarımının İlkeleri ve Kullanılabilirlik Testleri Üzerine Uygulamalı Atölye", 13, "Nil Ergün", "Tasarım", 15, 9, enrolled: true),
        Entry(4, "Temel Matematik Atölyesi", 12, "Selin Uçar", "Kesirler, oranlar ve yüzdeler", 25, 11),
        Entry(5, "İngilizce Konuşma Kulübü", 14, "Okan Sezer", "Günlük konuşma", null, 37),
        Entry(6, "İstatistiğe Giriş", 12, "Selin Uçar", "Ortalama, yayılım ve olasılık", 30, 5),
        Entry(7, "Web Sayfası Hazırlama", 13, "Nil Ergün", "HTML ve CSS", 18, 17),
        Entry(8, "Algoritmalar ve Problem Çözme", 11, "Barış Tuna", null, 0, 0),
    ];

    private readonly List<AccountListing> _accounts =
    [
        new(AccountRole.Admin, 21, "Derya Kılınç", "Yönetim", false, true),
        new(AccountRole.Teacher, 11, "Barış Tuna", "Yazılım", false, true),
        new(AccountRole.Teacher, 13, "Nil Ergün", "Tasarım", false, true),
        new(AccountRole.Teacher, 15, "Ozan Kaplangiller Sarıkayaoğlu", "Fizik ve Astronomi", false, false),
        new(AccountRole.Student, 101, "Zeynep Arıcan", "zeynep.arican@example.com", false, true),
        new(AccountRole.Student, 102, "Kerem Soylu", "kerem.soylu@example.com", true, true),
        new(AccountRole.Student, 103, "Ada Yalçınkaya", "ada.yalcinkaya@example.com", false, false),
        new(AccountRole.Student, 104, "Mina Çetinel", "mina.cetinel@example.com", false, true),
    ];

    public UserSession Student { get; } = Session(AccountRole.Student, 101, "Zeynep Arıcan");

    public UserSession Teacher { get; } = Session(AccountRole.Teacher, 11, "Barış Tuna");

    public UserSession Admin { get; } = Session(AccountRole.Admin, 21, "Derya Kılınç");

    /// <summary>What the setup and recovery screens fill in from EGITIM_SETUP_KEY; nothing here.</summary>
    public SetupKeyPrefill GetEnvironmentKey() => new(null, false);

    public Task<CatalogResult> SearchCatalogAsync(UserSession session, CourseSearch search, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CatalogResult(_catalog, 23, 0, CourseSearch.DefaultPageSize));

    public Task<IReadOnlyList<CourseSummary>> GetCatalogAsync(UserSession session, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CourseSummary>>([.. _catalog.Select(e => e.Course)]);

    public Task<IReadOnlyList<CourseSummary>> GetTeachingAsync(UserSession session, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CourseSummary>>([.. _catalog.Select(e => e.Course).Where(c => c.TeacherId == session.UserId)]);

    public Task<IReadOnlyList<CourseSummary>> GetMyCoursesAsync(UserSession session, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CourseSummary>>([.. _catalog.Where(e => e.Enrolled).Select(e => e.Course)]);

    public Task<TeacherProfile> GetMyProfileAsync(UserSession session, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TeacherProfile(session.UserId, session.DisplayName, "Yazılım"));

    public Task<CourseDetails> GetDetailsAsync(UserSession session, int courseId, CancellationToken cancellationToken = default)
    {
        var course = _catalog.Single(e => e.Course.Id == courseId).Course;
        var teacher = session.Role == AccountRole.Teacher;
        return Task.FromResult(new CourseDetails(
            course.Id,
            course.Name,
            course.Topic,
            course.TeacherName,
            NotesVisible: true,
            [
                "Haftalık ödev: döngülerle çarpım tablosu yazdırın.",
                "Perşembe dersinde kısa bir tekrar sınavı yapılacak.",
                "Kaynak kitabın 3. bölümünü okuyup sorularınızı not alın.",
            ],
            teacher ? ["Zeynep Arıcan", "Mina Çetinel", "Ada Yalçınkaya", "Kerem Soylu", "Can Aydemir"] : null,
            course.StudentCount,
            course.Capacity));
    }

    public Task<IReadOnlyList<string>> GetStudentsAsync(UserSession session, int courseId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(["Zeynep Arıcan", "Mina Çetinel", "Ada Yalçınkaya", "Kerem Soylu", "Can Aydemir", "Ece Doğrusöz Karabekiroğlu"]);

    public Task<AccountSearchResult> SearchAccountsAsync(UserSession admin, AccountSearch search, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AccountSearchResult(_accounts, 46, 0, AccountSearch.DefaultPageSize));

    public Task<RecoveryCode> CreateTeacherAsync(UserSession admin, string? fullName, string? specialty, CancellationToken cancellationToken = default) => Code();

    public Task<RecoveryCode> CreateStudentAsync(UserSession admin, string? fullName, string? email, CancellationToken cancellationToken = default) => Code();

    public Task<RecoveryCode> IssueResetCodeAsync(UserSession admin, AccountRole role, int accountId, CancellationToken cancellationToken = default) => Code();

    public Task<RecoveryCode> IssueStudentCodeAsync(UserSession teacher, string? studentName, CancellationToken cancellationToken = default) => Code();

    public Task EnrollAsync(UserSession session, int courseId, CancellationToken cancellationToken = default)
    {
        var index = _catalog.FindIndex(e => e.Course.Id == courseId);
        var course = _catalog[index].Course;
        _catalog[index] = new CatalogEntry(course with { StudentCount = course.StudentCount + 1 }, Enrolled: true);
        return Task.CompletedTask;
    }

    public Task LeaveAsync(UserSession session, int courseId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<int> CreateAsync(UserSession session, string? name, string? topic, int? capacity, CancellationToken cancellationToken = default) => Task.FromResult(99);

    public Task UpdateAsync(UserSession session, int courseId, string? name, string? topic, int? capacity, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(UserSession session, int courseId, int confirmedStudents, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AddNoteAsync(UserSession session, int courseId, string? content, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SetDisabledAsync(UserSession admin, AccountRole role, int accountId, bool disabled, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DemoteAdminAsync(UserSession admin, int accountId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<SetupStage> GetStageAsync(CancellationToken cancellationToken = default) => Task.FromResult(SetupStage.Completed);

    // The screens are drawn without anyone signing in; these never run.
    public Task<UserSession> SignInStudentAsync(string? fullName, string? password, CancellationToken cancellationToken = default) => Unused<UserSession>();

    public Task<UserSession> SignInTeacherAsync(string? fullName, string? password, CancellationToken cancellationToken = default) => Unused<UserSession>();

    public Task RegisterStudentAsync(string? fullName, string? email, string? password, CancellationToken cancellationToken = default) => Unused<bool>();

    public Task ChangePasswordAsync(UserSession session, string? currentPassword, string? newPassword, string? newPasswordRepeat, CancellationToken cancellationToken = default) => Unused<bool>();

    public Task<UserSession> RecoverStudentAsync(string? fullName, string? code, string? password, string? passwordRepeat, CancellationToken cancellationToken = default) => Unused<UserSession>();

    public Task<UserSession> RecoverTeacherAsync(string? setupKey, string? fullName, string? password, string? passwordRepeat, CancellationToken cancellationToken = default) => Unused<UserSession>();

    public Task<UserSession> CreateFirstAdminAsync(string? setupKey, string? fullName, string? specialty, string? password, string? passwordRepeat, CancellationToken cancellationToken = default) => Unused<UserSession>();

    private static Task<RecoveryCode> Code() => Task.FromResult(new RecoveryCode(SampleCode, SampleExpiry));

    private static Task<T> Unused<T>() => Task.FromException<T>(new InvalidOperationException("Not used while capturing."));

    private static CatalogEntry Entry(int id, string name, int teacherId, string teacher, string? topic, int? capacity, int students, bool enrolled = false) =>
        new(new CourseSummary(id, name, teacherId, teacher, topic, capacity, students), enrolled);

    /// <summary>A session as the services would issue it; its constructor is internal to the business layer.</summary>
    private static UserSession Session(AccountRole role, int id, string name) =>
        (UserSession)Activator.CreateInstance(
            typeof(UserSession),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [role, id, name, 0],
            culture: null)!;
}
