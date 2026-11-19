namespace OnlineEgitim.Business.Validation;

/// <summary>
/// A student registration. Name and e-mail address are cleaned with <see cref="InputRules.Clean"/>
/// (trimmed, <see langword="null"/> when missing); the password is kept exactly as typed.
/// </summary>
internal sealed record RegistrationInput(string? FullName, string? Email, string? Password)
{
    public static RegistrationInput Create(string? fullName, string? email, string? password) =>
        new(InputRules.Clean(fullName), InputRules.Clean(email), password);
}

/// <summary>A sign-in attempt of either role. The name is cleaned; the password is kept as typed.</summary>
internal sealed record SignInInput(string? FullName, string? Password)
{
    public static SignInInput Create(string? fullName, string? password) =>
        new(InputRules.Clean(fullName), password);
}

/// <summary>The first teacher of the first-run setup. Name and specialty are cleaned; passwords are kept as typed.</summary>
internal sealed record FirstTeacherInput(string? FullName, string? Specialty, string? Password, string? PasswordRepeat)
{
    public static FirstTeacherInput Create(string? fullName, string? specialty, string? password, string? passwordRepeat) =>
        new(InputRules.Clean(fullName), InputRules.Clean(specialty), password, passwordRepeat);
}

/// <summary>
/// The first password of an account without one, with its proof: a recovery code or a setup key. The
/// name is cleaned; proof and passwords are kept as typed.
/// </summary>
internal sealed record NewPasswordInput(string? FullName, string? Proof, string? Password, string? PasswordRepeat)
{
    public static NewPasswordInput Create(string? fullName, string? proof, string? password, string? passwordRepeat) =>
        new(InputRules.Clean(fullName), proof, password, passwordRepeat);
}

/// <summary>The student a teacher issues a recovery code for. The name is cleaned.</summary>
internal sealed record StudentNameInput(string? FullName)
{
    public static StudentNameInput Create(string? fullName) => new(InputRules.Clean(fullName));
}

/// <summary>A new or changed course. Name and topic are cleaned; no capacity means no limit.</summary>
internal sealed record CourseInput(string? Name, string? Topic, int? Capacity)
{
    public static CourseInput Create(string? name, string? topic, int? capacity = null) =>
        new(InputRules.Clean(name), InputRules.Clean(topic), capacity);
}

/// <summary>A note for a course. The content is cleaned (trimmed at both ends, inner line breaks kept).</summary>
internal sealed record NoteInput(int CourseId, string? Content)
{
    public static NoteInput Create(int courseId, string? content) => new(courseId, InputRules.Clean(content));
}
