using OnlineEgitim.Business.Services;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI.Tests.Support;

/// <summary>Creates every screen with test doubles for its dependencies, so no window, icon or database is involved.</summary>
internal static class TestForms
{
    /// <summary>The screens of the application by class name.</summary>
    public static readonly string[] Names =
    [
        nameof(GirisForm), nameof(OgretmenGirisForm), nameof(KayitForm), nameof(IlkKurulumForm), nameof(HesapKurtarmaForm),
        nameof(SifreDegistirForm), nameof(MainForm), nameof(KursDetayForm), nameof(KursNotlariForm), nameof(OgretmenForm),
        nameof(KursEklemeForm), nameof(KurtarmaKoduForm), nameof(YoneticiForm),
    ];

    public static AppDialog Create(string name) => name switch
    {
        nameof(GirisForm) => new GirisForm(new PendingAuthService(), new NoFormsFactory()),
        nameof(OgretmenGirisForm) => new OgretmenGirisForm(new PendingAuthService()),
        nameof(KayitForm) => new KayitForm(new PendingAuthService()),
        nameof(IlkKurulumForm) => new IlkKurulumForm(new PendingSetupService()),
        nameof(HesapKurtarmaForm) => new HesapKurtarmaForm(new PendingRecoveryService(), new PendingSetupService()),
        nameof(SifreDegistirForm) => new SifreDegistirForm(new PendingAuthService(), Sessions.Student),
        nameof(MainForm) => StudentPanel(),
        nameof(KursDetayForm) => CourseDetails(),
        nameof(KursNotlariForm) => new KursNotlariForm(new ControlledEnrollmentService(), Sessions.Student),
        nameof(OgretmenForm) => TeacherPanel(),
        nameof(KursEklemeForm) => new KursEklemeForm(new ControlledCourseService(), Sessions.Teacher),
        nameof(KurtarmaKoduForm) => new KurtarmaKoduForm(new PendingRecoveryService(), Sessions.Teacher),
        nameof(YoneticiForm) => AdminPanel(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    /// <summary>The student panel with a loaded catalogue whose courses have long names.</summary>
    public static MainForm StudentPanel(ControlledCourseService? courses = null, IUserPrompts? prompts = null)
    {
        courses ??= new ControlledCourseService();
        var form = new MainForm(courses, new ControlledEnrollmentService(), new NoFormsFactory(), Sessions.Student);
        if (prompts is not null)
        {
            form.Prompts = prompts;
        }

        Assert.True(form.LoadAsync().IsCompletedSuccessfully);
        return form;
    }

    public static OgretmenForm TeacherPanel(IUserPrompts? prompts = null)
    {
        var form = new OgretmenForm(new ControlledCourseService(), new ControlledEnrollmentService(), new NoFormsFactory(), Sessions.Teacher);
        if (prompts is not null)
        {
            form.Prompts = prompts;
        }

        Assert.True(form.LoadAsync().IsCompletedSuccessfully);
        return form;
    }

    public static KursDetayForm CourseDetails()
    {
        var courses = new ControlledCourseService
        {
            Details = new CourseDetails(1, "Temel Programlama", "Döngüler", "Deniz Örnek", true, ["İlk not", "İkinci not"], ["Mert Deneme"], 1, 20),
        };
        var form = new KursDetayForm(courses, Sessions.Teacher, 1);
        Assert.True(form.LoadAsync().Result);
        return form;
    }

    public static YoneticiForm AdminPanel(IUserPrompts? prompts = null)
    {
        var form = new YoneticiForm(new ListedAdministrationService(), new NoFormsFactory(), Sessions.Admin);
        if (prompts is not null)
        {
            form.Prompts = prompts;
        }

        Assert.True(form.LoadAsync().IsCompletedSuccessfully);
        return form;
    }
}

/// <summary>Answers account searches with a fixed page; writes are refused, as no test here sends one.</summary>
internal sealed class ListedAdministrationService : IAdministrationService
{
    public static readonly AccountListing LongName = new(
        AccountRole.Student,
        7,
        "Ayşegül Karamanlıoğlu Büyükçekmeceli Taşdelenoğulları Yıldırımoğlu",
        "aysegul.karamanlioglu.buyukcekmeceli@example.com",
        true,
        false);

    public Task<AccountSearchResult> SearchAccountsAsync(UserSession admin, AccountSearch search, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AccountSearchResult(
            [
                new AccountListing(AccountRole.Admin, 1, "Ayla Yönetici", "Yönetim", false, true),
                new AccountListing(AccountRole.Teacher, 2, "Deniz Örnek", "Yazılım", false, true),
                LongName,
            ],
            3,
            0,
            AccountSearch.DefaultPageSize));

    public Task<RecoveryCode> CreateTeacherAsync(UserSession admin, string? fullName, string? specialty, CancellationToken cancellationToken = default) => Refused<RecoveryCode>();

    public Task<RecoveryCode> CreateStudentAsync(UserSession admin, string? fullName, string? email, CancellationToken cancellationToken = default) => Refused<RecoveryCode>();

    public Task SetDisabledAsync(UserSession admin, AccountRole role, int accountId, bool disabled, CancellationToken cancellationToken = default) => Refused<bool>();

    public Task DemoteAdminAsync(UserSession admin, int accountId, CancellationToken cancellationToken = default) => Refused<bool>();

    public Task<RecoveryCode> IssueResetCodeAsync(UserSession admin, AccountRole role, int accountId, CancellationToken cancellationToken = default) => Refused<RecoveryCode>();

    private static Task<T> Refused<T>() => Task.FromException<T>(new Business.BusinessRuleException("Bu testte yazılmaz."));
}
