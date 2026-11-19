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

    public Task<int> CreateAsync(UserSession session, string? name, string? topic, CancellationToken cancellationToken = default)
    {
        CreateCalls++;
        _create = new TaskCompletionSource<int>();
        return _create.Task;
    }

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

    public string? KeyFilePath { get; set; } = @"C:\Kurulum\setup-key.txt";

    public Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<bool> IsTeacherMissingAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public string? IssueSetupKey() => KeyFilePath;

    public Task<UserSession> CreateFirstTeacherAsync(
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
