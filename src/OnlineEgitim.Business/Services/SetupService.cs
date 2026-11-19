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
/// password); whoever installs the application creates the first administrator with a password of
/// their own. Once the database has accounts but no administrator and no teacher who can sign in with
/// a password, the founder's authority is a setup key that only the database owner can create
/// (docs/issue-setup-key.sql): valid for 24 hours, usable once from any computer, and accepted only
/// while no teacher or administrator can sign in with a password. The application cannot create keys,
/// and a database whose teachers sign in with passwords gets its first administrator from the database
/// owner (docs/promote-admin.sql).
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
    /// Creates the first administrator account and signs it in. An empty database needs no key; a
    /// database with accounts needs the setup key. The key is used up in the same transaction that
    /// creates the administrator. Refused once an administrator exists or a teacher can sign in with a
    /// password, also when two setups run at the same time.
    /// </summary>
    Task<UserSession> CreateFirstAdminAsync(
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
        "Bu veritabanında hesaplar olduğu için ilk yönetici hesabı yalnızca kurulum anahtarıyla oluşturulabilir.";
    internal const string SetupKeyNotAllowed =
        "Şifreyle giriş yapabilen bir öğretmen veya yönetici hesabı olduğu için kurulum anahtarı kabul edilmez. "
        + "Mevcut bir öğretmeni yönetici yapmak için veritabanı sahibi docs/promote-admin.sql betiğini çalıştırır.";
    internal const string AlreadyDone = "Kurulum zaten tamamlanmış. Lütfen giriş yapın.";

    public async Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default) =>
        !await accounts.AnyAccountAsync(cancellationToken).ConfigureAwait(false);

    public Task<SetupStage> GetStageAsync(CancellationToken cancellationToken = default) =>
        accounts.GetSetupStageAsync(cancellationToken);

    public SetupKeyPrefill GetEnvironmentKey() => new(environment.Key, environment.IsSet && !environment.IsValid);

    public async Task<UserSession> CreateFirstAdminAsync(
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
        var admin = new Teacher
        {
            FullName = name,
            Specialty = field,
            PasswordHash = passwordHasher.Hash(input.Password!),
            PasswordFormat = PasswordFormat.Pbkdf2,
            Role = AccountRole.Admin,
        };

        // The repository decides atomically and checks the key inside its transaction; a check here
        // would leave a window for a second setup. A key is used up only by the write it authorized.
        var result = await accounts.AddFirstAdminAsync(admin, attempt, cancellationToken).ConfigureAwait(false);
        switch (result.Status)
        {
            case FirstAdminStatus.Created:
                audit.Record(AuditEvent.FirstAdminCreated, AccountRole.Admin);
                return new UserSession(AccountRole.Admin, result.AdminId, name);
            case FirstAdminStatus.NotAuthorized:
                audit.Record(AuditEvent.SetupRefused, null);
                await setupKeys.DelayAfterRefusalAsync(cancellationToken).ConfigureAwait(false);
                throw new BusinessRuleException(SetupKeyInvalid);
            case FirstAdminStatus.KeyRequired:
                audit.Record(AuditEvent.SetupRefused, null);
                throw new BusinessRuleException(SetupKeyRequired);
            case FirstAdminStatus.SetupCompleted when attempt is not null:
                audit.Record(AuditEvent.SetupRefused, null);
                throw new BusinessRuleException(SetupKeyNotAllowed);
            default:
                throw new BusinessRuleException(AlreadyDone);
        }
    }
}
