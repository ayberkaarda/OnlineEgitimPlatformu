using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FluentValidation;
using OnlineEgitim.Business.Security;

namespace OnlineEgitim.Business.Validation;

/// <summary>
/// User facing (Turkish) validation messages. They name fields and limits only, never the entered
/// values, so they can be shown and logged as they are.
/// </summary>
internal static class ValidationMessages
{
    /// <summary>Sign-in and recovery: something is missing or cannot belong to an account.</summary>
    public const string InputRequired = "Lütfen tüm alanları doldurun.";

    public const string RegisterFieldsRequired = "Lütfen tüm alanları doldurunuz.";
    public const string EmailInvalid = "Lütfen geçerli bir e-posta adresi girin.";
    public const string PasswordsDoNotMatch = "Şifreler uyuşmuyor.";
    public const string StudentNameRequired = "Lütfen öğrencinin adını girin.";
    public const string CourseFieldsRequired = "Lütfen tüm alanları doldurun.";
    public const string NoteRequired = "Lütfen bir kurs seçin ve not içeriğini girin.";
    public const string SearchInvalid = "Arama ölçütleri geçersiz. Daha kısa bir metinle yeniden deneyin.";

    public static readonly string NameInvalid = string.Create(
        CultureInfo.InvariantCulture,
        $"Ad soyad yalnızca harf, kesme işareti, nokta, kısa çizgi ve tek boşluk içerebilir ve en fazla {InputRules.PersonNameMaxLength} karakter olabilir.");

    public static readonly string PasswordInvalid = string.Create(
        CultureInfo.InvariantCulture,
        $"Şifre en az {PasswordPolicy.MinLength}, en fazla {PasswordPolicy.MaxLength} karakter olmalıdır.");

    public static readonly string SpecialtyInvalid = string.Create(
        CultureInfo.InvariantCulture,
        $"Uzmanlık alanı en fazla {InputRules.SpecialtyMaxLength} karakter olabilir.");

    public static readonly string CourseNameTooLong = string.Create(
        CultureInfo.InvariantCulture,
        $"Kurs adı en fazla {InputRules.CourseNameMaxLength} karakter olabilir.");

    public static readonly string TopicTooLong = string.Create(
        CultureInfo.InvariantCulture,
        $"Konu en fazla {InputRules.TopicMaxLength} karakter olabilir.");

    public static readonly string CapacityInvalid = string.Create(
        CultureInfo.InvariantCulture,
        $"Kontenjan 0 ile {InputRules.CourseCapacityMax} arasında olmalıdır; sınırsız kontenjan için boş bırakın.");

    public static readonly string NoteTooLong = string.Create(
        CultureInfo.InvariantCulture,
        $"Not en fazla {InputRules.NoteMaxLength} karakter olabilir.");
}

internal static class ValidationRuleExtensions
{
    /// <summary>Longest proof (recovery code or setup key with separators) a recovery accepts.</summary>
    public const int ProofMaxLength = 128;

    /// <summary>
    /// Validates <paramref name="input"/> and throws a <see cref="BusinessRuleException"/> carrying the
    /// first failure message, so the user sees one clear message at a time.
    /// </summary>
    public static void ValidateOrThrow<T>(this IValidator<T> validator, T input)
    {
        ArgumentNullException.ThrowIfNull(validator);

        var result = validator.Validate(input);
        if (!result.IsValid)
        {
            throw new BusinessRuleException(result.Errors[0].ErrorMessage);
        }
    }

    /// <summary>A password chosen for a new account or a first password: 8 to 64 characters.</summary>
    public static bool IsValidNewPassword([NotNullWhen(true)] string? password) =>
        password is { Length: >= PasswordPolicy.MinLength and <= PasswordPolicy.MaxLength };

    /// <summary>
    /// Sign-in only bounds the password by what an existing account can hold; the rules of
    /// <see cref="PasswordPolicy"/> for new passwords would lock out legacy accounts here.
    /// </summary>
    public static bool IsAcceptableSignInPassword([NotNullWhen(true)] string? password) =>
        !string.IsNullOrEmpty(password) && password.Length <= PasswordPolicy.SignInMaxLength;
}

/// <summary>
/// Registration, in this order: every field present, a person's name, an e-mail address, a password of
/// 8 to 64 characters.
/// </summary>
internal sealed class RegistrationInputValidator : AbstractValidator<RegistrationInput>
{
    public RegistrationInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r)
            .Must(r => r.FullName is not null && r.Email is not null && !string.IsNullOrWhiteSpace(r.Password))
            .WithMessage(ValidationMessages.RegisterFieldsRequired);
        RuleFor(r => r.FullName)
            .Must(InputRules.IsValidPersonName).WithMessage(ValidationMessages.NameInvalid);
        RuleFor(r => r.Email)
            .Must(InputRules.IsValidEmail).WithMessage(ValidationMessages.EmailInvalid);
        RuleFor(r => r.Password)
            .Must(ValidationRuleExtensions.IsValidNewPassword).WithMessage(ValidationMessages.PasswordInvalid);
    }
}

/// <summary>
/// Sign-in: a name that fits an account and a password an existing account can have. Every problem gets
/// the same message, so the answer says nothing about the account.
/// </summary>
internal sealed class SignInInputValidator : AbstractValidator<SignInInput>
{
    public SignInInputValidator()
    {
        RuleFor(s => s)
            .Must(s => s.FullName is { Length: <= InputRules.PersonNameMaxLength }
                && ValidationRuleExtensions.IsAcceptableSignInPassword(s.Password))
            .WithMessage(ValidationMessages.InputRequired);
    }
}

/// <summary>
/// The first teacher, in this order: every field present, a person's name, a specialty of at most 100
/// characters without control characters, two equal passwords, a password of 8 to 64 characters.
/// </summary>
internal sealed class FirstTeacherInputValidator : AbstractValidator<FirstTeacherInput>
{
    public FirstTeacherInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t)
            .Must(t => t.FullName is not null && t.Specialty is not null && !string.IsNullOrWhiteSpace(t.Password))
            .WithMessage(ValidationMessages.InputRequired);
        RuleFor(t => t.FullName)
            .Must(InputRules.IsValidPersonName).WithMessage(ValidationMessages.NameInvalid);
        RuleFor(t => t.Specialty)
            .Must(s => InputRules.IsValidText(s, InputRules.SpecialtyMaxLength)).WithMessage(ValidationMessages.SpecialtyInvalid);
        RuleFor(t => t.PasswordRepeat)
            .Must((t, repeat) => string.Equals(t.Password, repeat, StringComparison.Ordinal)).WithMessage(ValidationMessages.PasswordsDoNotMatch);
        RuleFor(t => t.Password)
            .Must(ValidationRuleExtensions.IsValidNewPassword).WithMessage(ValidationMessages.PasswordInvalid);
    }
}

/// <summary>
/// A first password with a recovery code or a setup key, in this order: name, proof (at most 128
/// characters) and password present, a name that fits an account, two equal passwords, a password of 8
/// to 64 characters. Whether the proof is right is decided by the service, not here.
/// </summary>
internal sealed class NewPasswordInputValidator : AbstractValidator<NewPasswordInput>
{
    public NewPasswordInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p)
            .Must(p => p.FullName is not null
                && !string.IsNullOrWhiteSpace(p.Proof)
                && !string.IsNullOrWhiteSpace(p.Password)
                && p.Proof.Length <= ValidationRuleExtensions.ProofMaxLength)
            .WithMessage(ValidationMessages.InputRequired);
        RuleFor(p => p.FullName)
            .Must(n => n is { Length: <= InputRules.PersonNameMaxLength }).WithMessage(ValidationMessages.NameInvalid);
        RuleFor(p => p.PasswordRepeat)
            .Must((p, repeat) => string.Equals(p.Password, repeat, StringComparison.Ordinal)).WithMessage(ValidationMessages.PasswordsDoNotMatch);
        RuleFor(p => p.Password)
            .Must(ValidationRuleExtensions.IsValidNewPassword).WithMessage(ValidationMessages.PasswordInvalid);
    }
}

/// <summary>
/// A password change, in this order: the current and the new password present (the current one at most
/// as long as an existing password can be), two equal new passwords, a new password of 8 to 64 characters.
/// </summary>
internal sealed class ChangePasswordInputValidator : AbstractValidator<ChangePasswordInput>
{
    public ChangePasswordInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p)
            .Must(p => ValidationRuleExtensions.IsAcceptableSignInPassword(p.CurrentPassword) && !string.IsNullOrWhiteSpace(p.NewPassword))
            .WithMessage(ValidationMessages.InputRequired);
        RuleFor(p => p.NewPasswordRepeat)
            .Must((p, repeat) => string.Equals(p.NewPassword, repeat, StringComparison.Ordinal)).WithMessage(ValidationMessages.PasswordsDoNotMatch);
        RuleFor(p => p.NewPassword)
            .Must(ValidationRuleExtensions.IsValidNewPassword).WithMessage(ValidationMessages.PasswordInvalid);
    }
}

/// <summary>The student of a recovery code: a name that fits an account.</summary>
internal sealed class StudentNameInputValidator : AbstractValidator<StudentNameInput>
{
    public StudentNameInputValidator()
    {
        RuleFor(s => s.FullName)
            .Must(n => n is { Length: <= InputRules.PersonNameMaxLength }).WithMessage(ValidationMessages.StudentNameRequired);
    }
}

/// <summary>
/// A course, in this order: name and topic present, a name of at most 100 and a topic of at most 200
/// characters, both without control characters, and no capacity (no limit) or 0 to 1000 seats.
/// </summary>
internal sealed class CourseInputValidator : AbstractValidator<CourseInput>
{
    public CourseInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c)
            .Must(c => c.Name is not null && c.Topic is not null).WithMessage(ValidationMessages.CourseFieldsRequired);
        RuleFor(c => c.Name)
            .Must(n => InputRules.IsValidText(n, InputRules.CourseNameMaxLength)).WithMessage(ValidationMessages.CourseNameTooLong);
        RuleFor(c => c.Topic)
            .Must(t => InputRules.IsValidText(t, InputRules.TopicMaxLength)).WithMessage(ValidationMessages.TopicTooLong);
        RuleFor(c => c.Capacity)
            .Must(c => c is null or (>= 0 and <= InputRules.CourseCapacityMax)).WithMessage(ValidationMessages.CapacityInvalid);
    }
}

/// <summary>A teacher account, in this order: both fields present, a person's name, a specialty of at most 100 characters.</summary>
internal sealed class TeacherAccountInputValidator : AbstractValidator<TeacherAccountInput>
{
    public TeacherAccountInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t)
            .Must(t => t.FullName is not null && t.Specialty is not null).WithMessage(ValidationMessages.InputRequired);
        RuleFor(t => t.FullName)
            .Must(InputRules.IsValidPersonName).WithMessage(ValidationMessages.NameInvalid);
        RuleFor(t => t.Specialty)
            .Must(s => InputRules.IsValidText(s, InputRules.SpecialtyMaxLength)).WithMessage(ValidationMessages.SpecialtyInvalid);
    }
}

/// <summary>A student account, in this order: both fields present, a person's name, an e-mail address.</summary>
internal sealed class StudentAccountInputValidator : AbstractValidator<StudentAccountInput>
{
    public StudentAccountInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(s => s)
            .Must(s => s.FullName is not null && s.Email is not null).WithMessage(ValidationMessages.InputRequired);
        RuleFor(s => s.FullName)
            .Must(InputRules.IsValidPersonName).WithMessage(ValidationMessages.NameInvalid);
        RuleFor(s => s.Email)
            .Must(InputRules.IsValidEmail).WithMessage(ValidationMessages.EmailInvalid);
    }
}

/// <summary>An account search: a text no longer than a name or address, a known role or none, a page from 0 to 1,000,000 of 1 to 50 accounts.</summary>
internal sealed class AccountSearchValidator : AbstractValidator<Services.AccountSearch>
{
    public AccountSearchValidator()
    {
        RuleFor(s => s)
            .Must(s => (InputRules.Clean(s.Text) is not { } text || text.Length <= InputRules.EmailMaxLength)
                && (s.Role is null || Enum.IsDefined(s.Role.Value))
                && s.PageIndex is >= 0 and <= CourseSearchValidator.MaxPageIndex
                && s.PageSize is >= 1 and <= Services.AccountSearch.MaxPageSize)
            .WithMessage(ValidationMessages.SearchInvalid);
    }
}

/// <summary>
/// A catalogue search: texts no longer than the columns they are compared with, a page from 0 to
/// 1,000,000 of 1 to 50 courses and a known enrollment filter.
/// </summary>
internal sealed class CourseSearchValidator : AbstractValidator<Services.CourseSearch>
{
    public const int MaxPageIndex = 1_000_000;

    public CourseSearchValidator()
    {
        RuleFor(s => s)
            .Must(s => Fits(s.Name, InputRules.CourseNameMaxLength)
                && Fits(s.Topic, InputRules.TopicMaxLength)
                && Fits(s.TeacherName, InputRules.PersonNameMaxLength)
                && s.PageIndex is >= 0 and <= MaxPageIndex
                && s.PageSize is >= 1 and <= Services.CourseSearch.MaxPageSize
                && Enum.IsDefined(s.Enrollment))
            .WithMessage(ValidationMessages.SearchInvalid);
    }

    private static bool Fits(string? text, int maxLength) => InputRules.Clean(text) is not { } clean || clean.Length <= maxLength;
}

/// <summary>
/// A note, in this order: a chosen course and some content, at most 500 characters with line breaks and
/// tabs but no other control characters.
/// </summary>
internal sealed class NoteInputValidator : AbstractValidator<NoteInput>
{
    public NoteInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(n => n)
            .Must(n => n.CourseId > 0 && n.Content is not null).WithMessage(ValidationMessages.NoteRequired);
        RuleFor(n => n.Content)
            .Must(c => InputRules.IsValidMultilineText(c, InputRules.NoteMaxLength)).WithMessage(ValidationMessages.NoteTooLong);
    }
}
