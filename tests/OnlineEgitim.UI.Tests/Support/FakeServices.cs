using OnlineEgitim.Business;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI.Tests.Support;

/// <summary>Sign-in and registration calls that stay pending until the test completes them.</summary>
internal sealed class PendingAuthService : IAuthService
{
    private TaskCompletionSource<UserSession> _signIn = new();
    private TaskCompletionSource _register = new();

    public int StudentSignInCalls { get; private set; }

    public int TeacherSignInCalls { get; private set; }

    public int RegisterCalls { get; private set; }

    public string? LastPassword { get; private set; }

    public Task<UserSession> SignInStudentAsync(string? fullName, string? password, CancellationToken cancellationToken = default)
    {
        StudentSignInCalls++;
        LastPassword = password;
        _signIn = new TaskCompletionSource<UserSession>();
        return _signIn.Task;
    }

    public Task<UserSession> SignInTeacherAsync(string? fullName, string? password, CancellationToken cancellationToken = default)
    {
        TeacherSignInCalls++;
        LastPassword = password;
        _signIn = new TaskCompletionSource<UserSession>();
        return _signIn.Task;
    }

    public Task RegisterStudentAsync(string? fullName, string? email, string? password, CancellationToken cancellationToken = default)
    {
        RegisterCalls++;
        _register = new TaskCompletionSource();
        return _register.Task;
    }

    public int ChangeCalls { get; private set; }

    public TaskCompletionSource Change { get; private set; } = new();

    public Task ChangePasswordAsync(UserSession session, string? currentPassword, string? newPassword, string? newPasswordRepeat, CancellationToken cancellationToken = default)
    {
        ChangeCalls++;
        LastPassword = currentPassword;
        Change = new TaskCompletionSource();
        return Change.Task;
    }

    /// <summary>Refuses the pending sign-in as wrong credentials would.</summary>
    public void RefuseSignIn() => _signIn.SetException(new BusinessRuleException("Giriş başarısız."));

    public void CompleteRegister() => _register.SetResult();
}

/// <summary>Course lists answered at once; writes stay pending.</summary>
internal sealed class ControlledCourseService : ICourseService
{
    private TaskCompletionSource<int> _create = new();
    private TaskCompletionSource _note = new();

    public List<CourseSummary> Courses { get; } =
    [
        new(1, "Temel Programlama", 1, "Deniz Örnek", "Döngüler"),
        new(2, "Veri Tabanına Giriş", 1, "Deniz Örnek", null),
    ];

    public CourseDetails? Details { get; set; }

    public int TeachingCalls { get; private set; }

    public int CreateCalls { get; private set; }

    public int NoteCalls { get; private set; }

    public Task<IReadOnlyList<CourseSummary>> GetCatalogAsync(UserSession session, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CourseSummary>>(Courses);

    /// <summary>When set, searches stay pending in <see cref="Searches"/> until the test answers them.</summary>
    public bool HoldSearches { get; set; }

    public List<(CourseSearch Search, TaskCompletionSource<CatalogResult> Answer)> Searches { get; } = [];

    public Task<CatalogResult> SearchCatalogAsync(UserSession session, CourseSearch search, CancellationToken cancellationToken = default)
    {
        var answer = new TaskCompletionSource<CatalogResult>();
        Searches.Add((search, answer));
        if (!HoldSearches)
        {
            answer.SetResult(Page(Courses, search.PageIndex));
        }

        return answer.Task;
    }

    /// <summary>A result with <paramref name="courses"/> on page <paramref name="pageIndex"/> of 20 each.</summary>
    public static CatalogResult Page(IEnumerable<CourseSummary> courses, int pageIndex = 0, int total = -1)
    {
        var items = courses.Select(c => new CatalogEntry(c, false)).ToList();
        return new CatalogResult(items, total < 0 ? items.Count : total, pageIndex, CourseSearch.DefaultPageSize);
    }

    public Task<IReadOnlyList<CourseSummary>> GetTeachingAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        TeachingCalls++;
        return Task.FromResult<IReadOnlyList<CourseSummary>>(Courses);
    }

    public Task<TeacherProfile> GetMyProfileAsync(UserSession session, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TeacherProfile(session.UserId, session.DisplayName, "Yazılım"));

    public Task<CourseDetails> GetDetailsAsync(UserSession session, int courseId, CancellationToken cancellationToken = default) =>
        Details is { } details
            ? Task.FromResult(details)
            : Task.FromException<CourseDetails>(new BusinessRuleException("Ders bulunamadı."));

    public int? LastCapacity { get; private set; }

    public List<string> Writes { get; } = [];

    public Task<int> CreateAsync(UserSession session, string? name, string? topic, int? capacity, CancellationToken cancellationToken = default)
    {
        CreateCalls++;
        LastCapacity = capacity;
        _create = new TaskCompletionSource<int>();
        return _create.Task;
    }

    public Task UpdateAsync(UserSession session, int courseId, string? name, string? topic, int? capacity, CancellationToken cancellationToken = default)
    {
        Writes.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"update:{courseId}:{name}:{capacity?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}"));
        _write = new TaskCompletionSource();
        return _write.Task;
    }

    public Task DeleteAsync(UserSession session, int courseId, int confirmedStudents, CancellationToken cancellationToken = default)
    {
        Writes.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"delete:{courseId}:{confirmedStudents}"));
        _write = new TaskCompletionSource();
        return _write.Task;
    }

    public void CompleteWrite() => _write.SetResult();

    private TaskCompletionSource _write = new();

    public Task AddNoteAsync(UserSession session, int courseId, string? content, CancellationToken cancellationToken = default)
    {
        NoteCalls++;
        _note = new TaskCompletionSource();
        return _note.Task;
    }

    public void CompleteCreate() => _create.SetResult(3);

    public void CompleteNote() => _note.SetResult();
}

/// <summary>Enrollment writes stay pending; the student lists of courses are answered by the test, in any order.</summary>
internal sealed class ControlledEnrollmentService : IEnrollmentService
{
    public int EnrollCalls { get; private set; }

    public List<CourseSummary> Mine { get; } = [];

    public List<(int CourseId, TaskCompletionSource<IReadOnlyList<string>> Answer)> StudentRequests { get; } = [];

    public Task EnrollAsync(UserSession session, int courseId, CancellationToken cancellationToken = default)
    {
        EnrollCalls++;
        return new TaskCompletionSource().Task;
    }

    public List<int> LeaveCalls { get; } = [];

    public TaskCompletionSource Leave { get; private set; } = new();

    public Task LeaveAsync(UserSession session, int courseId, CancellationToken cancellationToken = default)
    {
        LeaveCalls.Add(courseId);
        Leave = new TaskCompletionSource();
        return Leave.Task;
    }

    public Task<IReadOnlyList<CourseSummary>> GetMyCoursesAsync(UserSession session, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CourseSummary>>(Mine);

    public Task<IReadOnlyList<string>> GetStudentsAsync(UserSession session, int courseId, CancellationToken cancellationToken = default)
    {
        var answer = new TaskCompletionSource<IReadOnlyList<string>>();
        StudentRequests.Add((courseId, answer));
        return answer.Task;
    }
}

/// <summary>The first-run setup call stays pending until the test completes it.</summary>
internal sealed class PendingSetupService : ISetupService
{
    private TaskCompletionSource<UserSession> _create = new();

    public int CreateCalls { get; private set; }

    public string? LastKey { get; private set; }

    /// <summary>What EGITIM_SETUP_KEY fills in.</summary>
    public SetupKeyPrefill EnvironmentKey { get; set; } = new(null, false);

    public Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<SetupStage> GetStageAsync(CancellationToken cancellationToken = default) => Task.FromResult(SetupStage.FirstAdminNeedsKey);

    public SetupKeyPrefill GetEnvironmentKey() => EnvironmentKey;

    public Task<UserSession> CreateFirstAdminAsync(
        string? setupKey,
        string? fullName,
        string? specialty,
        string? password,
        string? passwordRepeat,
        CancellationToken cancellationToken = default)
    {
        CreateCalls++;
        LastKey = setupKey;
        _create = new TaskCompletionSource<UserSession>();
        return _create.Task;
    }

    public void Refuse() => _create.SetException(new BusinessRuleException("Şifreler uyuşmuyor."));
}

/// <summary>Recovery calls stay pending until the test completes them; the arguments are recorded.</summary>
internal sealed class PendingRecoveryService : IRecoveryService
{
    private TaskCompletionSource<RecoveryCode> _issue = new();
    private TaskCompletionSource<UserSession> _recover = new();

    public List<string> Calls { get; } = [];

    public Task<RecoveryCode> IssueStudentCodeAsync(UserSession teacher, string? studentName, CancellationToken cancellationToken = default)
    {
        Calls.Add("issue:" + studentName);
        _issue = new TaskCompletionSource<RecoveryCode>();
        return _issue.Task;
    }

    public Task<UserSession> RecoverStudentAsync(string? fullName, string? code, string? password, string? passwordRepeat, CancellationToken cancellationToken = default)
    {
        Calls.Add("student:" + fullName + "/" + code);
        _recover = new TaskCompletionSource<UserSession>();
        return _recover.Task;
    }

    public Task<UserSession> RecoverTeacherAsync(string? setupKey, string? fullName, string? password, string? passwordRepeat, CancellationToken cancellationToken = default)
    {
        Calls.Add("teacher:" + fullName + "/" + setupKey);
        _recover = new TaskCompletionSource<UserSession>();
        return _recover.Task;
    }

    public void CompleteIssue(string code) => _issue.SetResult(new RecoveryCode(code, new DateTime(2026, 3, 3, 10, 0, 0, DateTimeKind.Utc)));

    public void RefuseRecovery() => _recover.SetException(new BusinessRuleException("Kurtarma başarısız."));
}

/// <summary>Answers confirmations with yes and records every message instead of showing a dialog.</summary>
internal sealed class RecordingPrompts : IUserPrompts
{
    public List<string> Messages { get; } = [];

    public bool Confirm(string question)
    {
        Messages.Add(question);
        return true;
    }

    public void Inform(string message) => Messages.Add(message);

    public void Warn(string message) => Messages.Add(message);
}

internal sealed class NoFormsFactory : IFormFactory
{
    public T Create<T>(params object[] arguments)
        where T : Form => throw new InvalidOperationException("Tests do not open other forms.");
}

internal static class Sessions
{
    public static UserSession Student { get; } = new(AccountRole.Student, 1, "Mert Deneme");

    public static UserSession Teacher { get; } = new(AccountRole.Teacher, 1, "Deniz Örnek");
}
