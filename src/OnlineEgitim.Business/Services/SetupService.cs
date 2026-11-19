using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Services;

/// <summary>
/// First-run setup. A new database contains no account at all (there is no default or documented
/// password); the person installing the application creates the first teacher account with a password
/// of their own choosing.
/// </summary>
public interface ISetupService
{
    /// <summary>Whether no teacher account exists yet.</summary>
    Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the first teacher account and signs it in. Refused once any teacher exists, also when
    /// two setups run at the same time.
    /// </summary>
    Task<UserSession> CreateFirstTeacherAsync(
        string? fullName,
        string? specialty,
        string? password,
        string? passwordRepeat,
        CancellationToken cancellationToken = default);
}

internal sealed class SetupService(
    IAccountRepository accounts,
    IPasswordHasher passwordHasher,
    IAuditLog audit) : ISetupService
{
    internal const string FieldsRequired = "Lütfen tüm alanları doldurun.";
    internal const string PasswordsDoNotMatch = "Şifreler uyuşmuyor.";
    internal const string AlreadyDone = "Kurulum zaten tamamlanmış. Lütfen giriş yapın.";
    internal const string SpecialtyInvalid = "Uzmanlık alanı en fazla 100 karakter olabilir.";

    public async Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default) =>
        !await accounts.AnyTeacherAsync(cancellationToken).ConfigureAwait(false);

    public async Task<UserSession> CreateFirstTeacherAsync(
        string? fullName,
        string? specialty,
        string? password,
        string? passwordRepeat,
        CancellationToken cancellationToken = default)
    {
        var name = InputRules.Clean(fullName);
        var field = InputRules.Clean(specialty);
        if (name is null || field is null || string.IsNullOrWhiteSpace(password))
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

        // The repository decides atomically; a check here would leave a window for a second setup.
        var id = await accounts.AddFirstTeacherAsync(teacher, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessRuleException(AlreadyDone);

        audit.Record(AuditEvent.FirstTeacherCreated, AccountRole.Teacher);
        return new UserSession(AccountRole.Teacher, id, name);
    }
}
