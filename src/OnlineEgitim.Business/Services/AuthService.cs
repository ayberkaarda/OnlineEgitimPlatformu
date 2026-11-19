using System.Globalization;
using FluentValidation;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Services;

public interface IAuthService
{
    /// <summary>Signs a student in with the name and password, as the original student sign-in did.</summary>
    /// <exception cref="BusinessRuleException">Wrong credentials, missing input or a locked account.</exception>
    Task<UserSession> SignInStudentAsync(string? fullName, string? password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs a teacher or an administrator in with the name and password (the staff sign-in, as the
    /// original teacher sign-in did); the session has the account's own role.
    /// </summary>
    /// <exception cref="BusinessRuleException">Wrong credentials, missing input, a locked or a disabled account.</exception>
    Task<UserSession> SignInTeacherAsync(string? fullName, string? password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new student; the password is stored as a hash. A name or e-mail address that is
    /// already registered ends the same way as a new registration (without a new account or any change
    /// to the existing one), so registration does not tell whether someone has an account. Only invalid
    /// input is refused.
    /// </summary>
    Task RegisterStudentAsync(string? fullName, string? email, string? password, CancellationToken cancellationToken = default);
}

internal sealed class AuthService(
    IAccountRepository accounts,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider,
    IAuditLog audit,
    IValidator<RegistrationInput> registrationValidator,
    IValidator<SignInInput> signInValidator) : IAuthService
{
    // Every refused sign-in (unknown name, wrong password, no password, locked or disabled account, a
    // lost race) gets this one answer and one audit event; the lockout rule itself is stated for everyone.
    internal static readonly string CredentialsInvalid = string.Create(
        CultureInfo.InvariantCulture,
        $"Giriş başarısız. Ad veya şifre hatalı. {LoginPolicy.MaxFailedAttempts} hatalı denemeden sonra giriş {LoginPolicy.LockoutDuration.TotalMinutes:0} dakika engellenir.");

    public Task<UserSession> SignInStudentAsync(string? fullName, string? password, CancellationToken cancellationToken = default) =>
        SignInAsync(AccountRole.Student, fullName, password, cancellationToken);

    public Task<UserSession> SignInTeacherAsync(string? fullName, string? password, CancellationToken cancellationToken = default) =>
        SignInAsync(AccountRole.Teacher, fullName, password, cancellationToken);

    public async Task RegisterStudentAsync(
        string? fullName,
        string? email,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var input = RegistrationInput.Create(fullName, email, password);
        registrationValidator.ValidateOrThrow(input);
        var name = input.FullName!;
        var mail = input.Email!;

        // Derived before the check, so a taken name or address costs as much as a new account.
        var student = new Student
        {
            FullName = name,
            Email = mail,
            PasswordHash = passwordHasher.Hash(input.Password!),
            PasswordFormat = PasswordFormat.Pbkdf2,
        };

        if (await accounts.StudentExistsAsync(name, mail, cancellationToken).ConfigureAwait(false))
        {
            audit.Record(AuditEvent.RegistrationNotCompleted, AccountRole.Student);
            return;
        }

        try
        {
            await accounts.AddStudentAsync(student, cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateAccountException)
        {
            // Registered concurrently between the check and the insert: the same outcome as a taken one.
            audit.Record(AuditEvent.RegistrationNotCompleted, AccountRole.Student);
            return;
        }

        audit.Record(AuditEvent.StudentRegistered, AccountRole.Student);
    }

    private async Task<UserSession> SignInAsync(
        AccountRole role,
        string? fullName,
        string? password,
        CancellationToken cancellationToken)
    {
        var input = SignInInput.Create(fullName, password);
        signInValidator.ValidateOrThrow(input);
        var name = input.FullName!;
        password = input.Password!;

        var account = await accounts.FindAsync(role, name, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            passwordHasher.SimulateVerification(password);
            throw Refused(role);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (account.LockoutEnd > now || account.Disabled)
        {
            // Same answer and about the same cost as a wrong password; nothing is written, so the
            // lock neither grows nor ends early, and a disabled account is not locked by guessing.
            passwordHasher.SimulateVerification(password);
            throw Refused(role);
        }

        var result = passwordHasher.Verify(password, account.PasswordHash, account.PasswordFormat);
        if (result == PasswordVerificationResult.Failed)
        {
            await accounts.RecordFailedLoginAsync(
                role,
                account.Id,
                LoginPolicy.MaxFailedAttempts,
                now + LoginPolicy.LockoutDuration,
                cancellationToken).ConfigureAwait(false);
            throw Refused(role);
        }

        string? newHash = null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // Plain text passwords were compared trimmed by the original application.
            var legacy = account.PasswordFormat == PasswordFormat.LegacyPlainText;
            newHash = passwordHasher.Hash(legacy ? password.Trim() : password);
        }

        // The session is issued only if the account is still unlocked and unchanged when the success
        // is written; a lock placed by a concurrent failure since the read wins.
        var recorded = await accounts.RecordSuccessfulLoginAsync(role, account, now, newHash, cancellationToken).ConfigureAwait(false);
        if (!recorded)
        {
            throw Refused(role);
        }

        if (newHash is not null)
        {
            audit.Record(AuditEvent.PasswordUpgraded, account.Role);
        }

        audit.Record(AuditEvent.SignInSucceeded, account.Role);
        return new UserSession(account.Role, account.Id, account.FullName, account.SessionVersion);
    }

    private BusinessRuleException Refused(AccountRole role)
    {
        audit.Record(AuditEvent.SignInFailed, role);
        return new BusinessRuleException(CredentialsInvalid);
    }
}
