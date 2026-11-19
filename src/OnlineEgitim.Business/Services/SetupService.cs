using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Services;

/// <summary>
/// First-run setup. A new database contains no account at all (there is no default or documented
/// password); the person installing the application creates the first teacher account with a password
/// of their own choosing. Creating it needs the local setup key (<see cref="ISetupKeyStore"/>): a
/// database without teachers alone does not make whoever opens the application its founder.
/// </summary>
public interface ISetupService
{
    /// <summary>
    /// Whether the database is empty (no teacher and no student), so the application opens the setup
    /// instead of the sign-in. A database with accounts keeps its normal sign-in.
    /// </summary>
    Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether no teacher account exists, so the first one can still be created with the setup key.</summary>
    Task<bool> IsTeacherMissingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes sure a setup key exists and returns the file that holds it, or <see langword="null"/> when
    /// the key is taken from the environment.
    /// </summary>
    string? IssueSetupKey();

    /// <summary>
    /// Creates the first teacher account and signs it in. Refused without the current setup key, and
    /// once any teacher exists, also when two setups run at the same time. The key is used up by the
    /// setup that creates the teacher.
    /// </summary>
    Task<UserSession> CreateFirstTeacherAsync(
        string? setupKey,
        string? fullName,
        string? specialty,
        string? password,
        string? passwordRepeat,
        CancellationToken cancellationToken = default);
}

internal sealed class SetupService(
    IAccountRepository accounts,
    IPasswordHasher passwordHasher,
    ISetupKeyStore setupKeys,
    IAuditLog audit) : ISetupService
{
    internal const string SetupKeyInvalid = "Kurulum anahtarı hatalı veya daha önce kullanılmış.";
    internal const string FieldsRequired = "Lütfen tüm alanları doldurun.";
    internal const string PasswordsDoNotMatch = "Şifreler uyuşmuyor.";
    internal const string AlreadyDone = "Kurulum zaten tamamlanmış. Lütfen giriş yapın.";
    internal const string SpecialtyInvalid = "Uzmanlık alanı en fazla 100 karakter olabilir.";

    public async Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default) =>
        !await accounts.AnyAccountAsync(cancellationToken).ConfigureAwait(false);

    public async Task<bool> IsTeacherMissingAsync(CancellationToken cancellationToken = default) =>
        !await accounts.AnyTeacherAsync(cancellationToken).ConfigureAwait(false);

    public string? IssueSetupKey()
    {
        setupKeys.EnsureIssued();
        return setupKeys.KeyFilePath;
    }

    public async Task<UserSession> CreateFirstTeacherAsync(
        string? setupKey,
        string? fullName,
        string? specialty,
        string? password,
        string? passwordRepeat,
        CancellationToken cancellationToken = default)
    {
        var name = InputRules.Clean(fullName);
        var field = InputRules.Clean(specialty);
        if (string.IsNullOrWhiteSpace(setupKey) || name is null || field is null || string.IsNullOrWhiteSpace(password))
        {
            throw new BusinessRuleException(FieldsRequired);
        }

        if (!InputRules.IsValidPersonName(name))
        {
            throw new BusinessRuleException(AuthService.NameInvalid);
        }

        if (!InputRules.IsValidText(field, InputRules.SpecialtyMaxLength))
        {
            throw new BusinessRuleException(SpecialtyInvalid);
        }

        if (!string.Equals(password, passwordRepeat, StringComparison.Ordinal))
        {
            throw new BusinessRuleException(PasswordsDoNotMatch);
        }

        if (password.Length is < PasswordPolicy.MinLength or > PasswordPolicy.MaxLength)
        {
            throw new BusinessRuleException(AuthService.PasswordInvalid);
        }

        var teacher = new Teacher
        {
            FullName = name,
            Specialty = field,
            PasswordHash = passwordHasher.Hash(password),
            PasswordFormat = PasswordFormat.Pbkdf2,
        };

        // The repository decides atomically and asks for the key inside its transaction; a check here
        // would leave a window for a second setup. A wrong key is not used up.
        var result = await accounts.AddFirstTeacherAsync(teacher, () => setupKeys.TryConsume(setupKey), cancellationToken).ConfigureAwait(false);
        switch (result.Status)
        {
            case FirstTeacherStatus.Created:
                audit.Record(AuditEvent.FirstTeacherCreated, AccountRole.Teacher);
                return new UserSession(AccountRole.Teacher, result.TeacherId, name);
            case FirstTeacherStatus.NotAuthorized:
                audit.Record(AuditEvent.SetupRefused, null);
                throw new BusinessRuleException(SetupKeyInvalid);
            default:
                throw new BusinessRuleException(AlreadyDone);
        }
    }
}
