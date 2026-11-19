using System.Globalization;
using FluentValidation;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Services;

/// <summary>A recovery code as it is handed over once; only its hash is stored.</summary>
public sealed record RecoveryCode(string Code, DateTime ExpiresUtc);

/// <summary>
/// Verified recovery of accounts that have no password (<see cref="PasswordFormat.None"/>: kept from the
/// original application, or created by an administrator with a code instead of a password). An empty
/// or any other password never signs them in; instead an authorized person issues a single use,
/// expiring code: a teacher for a student of their own courses, an administrator when creating the
/// account. While no teacher or administrator can sign in with a password, the founder's setup key
/// sets the first password of a staff account, or of a student who is enrolled in no course (such a
/// student has no teacher who could issue a code). An enrolled student is never recovered with the
/// key: the course's teacher issues the code. The account keeps its id and with it its courses and
/// enrollments.
/// </summary>
public interface IRecoveryService
{
    /// <summary>
    /// Issues a recovery code for the student without a password who signs in with
    /// <paramref name="studentName"/>, replacing an earlier code. Teachers only, and only for a student
    /// enrolled in at least one of the teacher's own courses; any other student gets the answer of a
    /// student that does not exist and keeps its earlier code.
    /// </summary>
    Task<RecoveryCode> IssueStudentCodeAsync(UserSession teacher, string? studentName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the first password of a student and signs the student in, with the code a teacher or an
    /// administrator issued or, while no teacher can sign in with a password, with the setup key (told
    /// apart by their form). The setup key works only for a student enrolled in no course; for an
    /// enrolled student it is refused like a wrong key and stays unused.
    /// </summary>
    Task<UserSession> RecoverStudentAsync(string? fullName, string? code, string? password, string? passwordRepeat, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the first password of a teacher or an administrator and signs the account in, with the code
    /// an administrator issued or with the setup key (told apart by their form). The setup key is refused
    /// while any teacher or administrator can sign in with a password.
    /// </summary>
    Task<UserSession> RecoverTeacherAsync(string? setupKey, string? fullName, string? password, string? passwordRepeat, CancellationToken cancellationToken = default);
}

internal sealed class RecoveryService(
    IAccountRepository accounts,
    IPasswordHasher passwordHasher,
    SetupKeyGate setupKeys,
    TimeProvider timeProvider,
    IAuditLog audit,
    ISessionGuard sessions,
    IValidator<StudentNameInput> studentNameValidator,
    IValidator<NewPasswordInput> newPasswordValidator) : IRecoveryService
{
    internal const string NoPasswordlessStudent = "Bu adla kayıtlı, şifresi olmayan bir öğrenci bulunamadı.";
    internal const string TeacherRecoveryFailed =
        "Şifre belirlenemedi. Kurulum anahtarını ve adı kontrol edin; yalnızca şifresi olmayan öğretmen hesaplarına şifre belirlenebilir.";
    internal const string SetupKeyRecoveryFailed =
        "Şifre belirlenemedi. Kurulum anahtarını ve adı kontrol edin; yalnızca şifresi olmayan hesaplara şifre belirlenebilir.";

    internal static readonly string StudentRecoveryFailed = string.Create(
        CultureInfo.InvariantCulture,
        $"Kurtarma başarısız. Ad veya kurtarma kodu hatalı ya da kodun süresi dolmuş. {LoginPolicy.MaxFailedAttempts} hatalı denemeden sonra giriş {LoginPolicy.LockoutDuration.TotalMinutes:0} dakika engellenir.");

    public async Task<RecoveryCode> IssueStudentCodeAsync(UserSession teacher, string? studentName, CancellationToken cancellationToken = default)
    {
        Access.RequireRole(teacher, AccountRole.Teacher, audit);
        await sessions.EnsureCurrentAsync(teacher, cancellationToken).ConfigureAwait(false);
        var input = StudentNameInput.Create(studentName);
        studentNameValidator.ValidateOrThrow(input);
        var name = input.FullName!;

        // One conditional write decides: a missing student, one with a password and a student who is
        // not enrolled in any course of this teacher get the same answer, and nothing changes.
        var code = CodeFormat.NewCode(RecoveryPolicy.CodeLength);
        var expires = timeProvider.GetUtcNow().UtcDateTime + RecoveryPolicy.CodeLifetime;
        var stored = await accounts.SetRecoveryCodeAsync(
            teacher.UserId,
            name,
            passwordHasher.Hash(CodeFormat.Normalize(code)),
            expires,
            cancellationToken).ConfigureAwait(false);
        if (!stored)
        {
            throw new BusinessRuleException(NoPasswordlessStudent);
        }

        audit.Record(AuditEvent.RecoveryCodeIssued, AccountRole.Teacher);
        return new RecoveryCode(code, expires);
    }

    public async Task<UserSession> RecoverStudentAsync(
        string? fullName,
        string? code,
        string? password,
        string? passwordRepeat,
        CancellationToken cancellationToken = default)
    {
        var name = ValidateNewPassword(fullName, code, password, passwordRepeat);
        if (SetupKeyPolicy.TryNormalize(code, out _))
        {
            // A setup key, not a code: the founder's way, for a student enrolled in no course only.
            return await RecoverWithSetupKeyAsync(AccountRole.Student, code, name, password!, cancellationToken).ConfigureAwait(false);
        }

        return await RecoverWithCodeAsync(AccountRole.Student, name, code!, password!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserSession> RecoverTeacherAsync(
        string? setupKey,
        string? fullName,
        string? password,
        string? passwordRepeat,
        CancellationToken cancellationToken = default)
    {
        var name = ValidateNewPassword(fullName, setupKey, password, passwordRepeat);
        if (SetupKeyPolicy.TryNormalize(setupKey, out _))
        {
            return await RecoverWithSetupKeyAsync(AccountRole.Teacher, setupKey, name, password!, cancellationToken).ConfigureAwait(false);
        }

        return await RecoverWithCodeAsync(AccountRole.Teacher, name, setupKey!, password!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A password with a single use code, for the account of <paramref name="table"/>'s table. Every
    /// refusal has one message and one audit event and costs one verification, whether the account
    /// exists, has a usable code, is locked or disabled; wrong codes count towards the lockout.
    /// </summary>
    private async Task<UserSession> RecoverWithCodeAsync(AccountRole table, string name, string code, string password, CancellationToken cancellationToken)
    {
        var given = CodeFormat.Normalize(code);
        var account = await accounts.FindRecoveryAsync(table, name, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (account is null || account.LockoutEnd > now || account.Disabled)
        {
            passwordHasher.SimulateVerification(given);
            throw Refused(table);
        }

        // Exactly one verification on every path, real or simulated: an account without a usable code
        // (none, expired, or a password set meanwhile) costs what a wrong code costs.
        var usableHash = account.PasswordFormat == PasswordFormat.None && account.RecoveryCodeExpires > now
            ? account.RecoveryCodeHash
            : null;
        bool verified;
        if (usableHash is null)
        {
            passwordHasher.SimulateVerification(given);
            verified = false;
        }
        else
        {
            verified = passwordHasher.Verify(given, usableHash, PasswordFormat.Pbkdf2) != PasswordVerificationResult.Failed;
        }

        if (!verified)
        {
            // Wrong codes count like wrong passwords, so a code cannot be guessed.
            await accounts.RecordFailedLoginAsync(
                table,
                account.Id,
                LoginPolicy.MaxFailedAttempts,
                now + LoginPolicy.LockoutDuration,
                cancellationToken).ConfigureAwait(false);
            throw Refused(table);
        }

        var completed = await accounts.CompleteRecoveryAsync(table, account, now, passwordHasher.Hash(password), cancellationToken).ConfigureAwait(false);
        var state = completed ? await accounts.GetSessionStateAsync(table, account.Id, cancellationToken).ConfigureAwait(false) : null;
        if (state is null)
        {
            throw Refused(table);
        }

        audit.Record(AuditEvent.AccountRecovered, state.Role);
        return new UserSession(state.Role, account.Id, account.FullName, state.SessionVersion);
    }

    /// <summary>
    /// The founder's recovery: the repository checks the key and uses it up in the transaction that
    /// writes the password, so a failed write leaves the key unused.
    /// </summary>
    private async Task<UserSession> RecoverWithSetupKeyAsync(AccountRole role, string? setupKey, string name, string password, CancellationToken cancellationToken)
    {
        var attempt = setupKeys.CreateAttempt(setupKey) ?? throw new BusinessRuleException(ValidationMessages.InputRequired);
        var result = await accounts.SetFirstPasswordWithSetupKeyAsync(role, name, passwordHasher.Hash(password), attempt, cancellationToken).ConfigureAwait(false);
        switch (result.Status)
        {
            case SetupKeyWriteStatus.Done:
                var state = await accounts.GetSessionStateAsync(role, result.AccountId, cancellationToken).ConfigureAwait(false);
                var sessionRole = state?.Role ?? role;
                audit.Record(AuditEvent.AccountRecovered, sessionRole);
                var displayName = role == AccountRole.Teacher
                    ? (await accounts.GetTeacherAsync(result.AccountId, cancellationToken).ConfigureAwait(false))?.FullName ?? name
                    : (await accounts.FindRecoveryAsync(AccountRole.Student, name, cancellationToken).ConfigureAwait(false))?.FullName ?? name;
                return new UserSession(sessionRole, result.AccountId, displayName, state?.SessionVersion ?? 0);
            case SetupKeyWriteStatus.SetupCompleted:
                audit.Record(AuditEvent.RecoveryFailed, null);
                throw new BusinessRuleException(SetupService.SetupKeyNotAllowed);
            default:
                audit.Record(AuditEvent.RecoveryFailed, null);
                await setupKeys.DelayAfterRefusalAsync(cancellationToken).ConfigureAwait(false);
                throw new BusinessRuleException(role == AccountRole.Teacher ? TeacherRecoveryFailed : SetupKeyRecoveryFailed);
        }
    }

    /// <summary>Checks the input of a first password and returns the cleaned name.</summary>
    private string ValidateNewPassword(string? fullName, string? proof, string? password, string? passwordRepeat)
    {
        var input = NewPasswordInput.Create(fullName, proof, password, passwordRepeat);
        newPasswordValidator.ValidateOrThrow(input);
        return input.FullName!;
    }

    private BusinessRuleException Refused(AccountRole table)
    {
        audit.Record(AuditEvent.RecoveryFailed, table);
        return new BusinessRuleException(StudentRecoveryFailed);
    }
}
