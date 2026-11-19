using FluentValidation;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Services;

/// <summary>What the EGITIM_SETUP_KEY environment variable fills in, or why it fills in nothing.</summary>
/// <param name="Key">The key to put in the key field, or <see langword="null"/>.</param>
/// <param name="Invalid">The variable is set to something that is not a setup key.</param>
public sealed record SetupKeyPrefill(string? Key, bool Invalid);

/// <summary>
/// First-run setup. A new database contains no account at all (there is no default or documented
/// password); whoever installs the application creates the first teacher with a password of their own.
/// Once the database has accounts but no teacher who can sign in with a password, the founder's
/// authority is a setup key that only the database owner can create (docs/issue-setup-key.sql): valid
/// for 24 hours, usable once from any computer, and accepted only while no teacher can sign in with a
/// password. The application cannot create keys.
/// </summary>
public interface ISetupService
{
    /// <summary>
    /// Whether the database is empty (no teacher and no student), so the application opens the setup
    /// instead of the sign-in. A database with accounts keeps its normal sign-in.
    /// </summary>
    Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default);

    /// <summary>Where the setup stands; decides which founder operations the sign-in offers.</summary>
    Task<SetupStage> GetStageAsync(CancellationToken cancellationToken = default);

    /// <summary>The key EGITIM_SETUP_KEY fills into the forms; checked against the database like a typed one.</summary>
    SetupKeyPrefill GetEnvironmentKey();

    /// <summary>
    /// Creates the first teacher account and signs it in. An empty database needs no key; a database
    /// with students needs the setup key. The key is used up in the same transaction that creates the
    /// teacher. Refused once any teacher exists, also when two setups run at the same time.
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
    SetupKeyGate setupKeys,
    SetupKeyEnvironment environment,
    IAuditLog audit,
    IValidator<FirstTeacherInput> validator) : ISetupService
{
    internal const string SetupKeyInvalid = "Kurulum anahtarı hatalı, süresi dolmuş veya daha önce kullanılmış.";
    internal const string SetupKeyRequired =
        "Bu veritabanında öğrenci hesapları olduğu için ilk öğretmen hesabı yalnızca kurulum anahtarıyla oluşturulabilir.";
    internal const string SetupKeyNotAllowed =
        "Şifreyle giriş yapabilen bir öğretmen hesabı olduğu için kurulum anahtarı kabul edilmez.";
    internal const string AlreadyDone = "Kurulum zaten tamamlanmış. Lütfen giriş yapın.";

    public async Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default) =>
        !await accounts.AnyAccountAsync(cancellationToken).ConfigureAwait(false);

    public Task<SetupStage> GetStageAsync(CancellationToken cancellationToken = default) =>
        accounts.GetSetupStageAsync(cancellationToken);

    public SetupKeyPrefill GetEnvironmentKey() => new(environment.Key, environment.IsSet && !environment.IsValid);

    public async Task<UserSession> CreateFirstTeacherAsync(
        string? setupKey,
        string? fullName,
        string? specialty,
        string? password,
        string? passwordRepeat,
        CancellationToken cancellationToken = default)
    {
        var input = FirstTeacherInput.Create(fullName, specialty, password, passwordRepeat);
        validator.ValidateOrThrow(input);
        var name = input.FullName!;
        var field = input.Specialty!;

        var attempt = setupKeys.CreateAttempt(setupKey);
        var teacher = new Teacher
        {
            FullName = name,
            Specialty = field,
            PasswordHash = passwordHasher.Hash(input.Password!),
            PasswordFormat = PasswordFormat.Pbkdf2,
        };

        // The repository decides atomically and checks the key inside its transaction; a check here
        // would leave a window for a second setup. A key is used up only by the write it authorized.
        var result = await accounts.AddFirstTeacherAsync(teacher, attempt, cancellationToken).ConfigureAwait(false);
        switch (result.Status)
        {
            case FirstTeacherStatus.Created:
                audit.Record(AuditEvent.FirstTeacherCreated, AccountRole.Teacher);
                return new UserSession(AccountRole.Teacher, result.TeacherId, name);
            case FirstTeacherStatus.NotAuthorized:
                audit.Record(AuditEvent.SetupRefused, null);
                await setupKeys.DelayAfterRefusalAsync(cancellationToken).ConfigureAwait(false);
                throw new BusinessRuleException(SetupKeyInvalid);
            case FirstTeacherStatus.KeyRequired:
                audit.Record(AuditEvent.SetupRefused, null);
                throw new BusinessRuleException(SetupKeyRequired);
            default:
                throw new BusinessRuleException(AlreadyDone);
        }
    }
}
