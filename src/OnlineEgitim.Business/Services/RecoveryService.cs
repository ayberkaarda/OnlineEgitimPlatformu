using System.Globalization;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Services;

/// <summary>A recovery code as it is handed to the student once; only its hash is stored.</summary>
public sealed record RecoveryCode(string Code, DateTime ExpiresUtc);

/// <summary>
/// Verified recovery of accounts that have no password (<see cref="PasswordFormat.None"/>, kept from the
/// original application). An empty or any other password never signs them in; instead a teacher issues
/// a single use, expiring code for a student of their own courses. While no teacher can sign in with a
/// password, the founder's setup key sets the first password of a teacher, or of a student (the only
/// way for a student who is enrolled in no course). The account keeps its id and with it its courses
/// and enrollments.
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
    /// Sets the first password of a student and signs the student in, with the code a teacher issued
    /// or, while no teacher can sign in with a password, with the setup key (told apart by their form).
    /// </summary>
    Task<UserSession> RecoverStudentAsync(string? fullName, string? code, string? password, string? passwordRepeat, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the first password of a teacher without one with the setup key and signs the teacher in.
    /// Refused while any teacher can sign in with a password.
    /// </summary>
    Task<UserSession> RecoverTeacherAsync(string? setupKey, string? fullName, string? password, string? passwordRepeat, CancellationToken cancellationToken = default);
}

internal sealed class RecoveryService(
    IAccountRepository accounts,
    IPasswordHasher passwordHasher,
    SetupKeyGate setupKeys,
    TimeProvider timeProvider,
    IAuditLog audit) : IRecoveryService
{
    internal const string StudentNameRequired = "Lütfen öğrencinin adını girin.";
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
        var name = InputRules.Clean(studentName);
        if (name is null || name.Length > InputRules.PersonNameMaxLength)
        {
            throw new BusinessRuleException(StudentNameRequired);
        }

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
        var name = InputRules.Clean(fullName);
        ValidateNewPassword(name, code, password, passwordRepeat);
        if (SetupKeyPolicy.TryNormalize(code, out _))
        {
            // A setup key, not a code: the founder's way, also for a student enrolled in no course.
            return await RecoverWithSetupKeyAsync(AccountRole.Student, code, name!, password!, cancellationToken).ConfigureAwait(false);
        }

        var given = CodeFormat.Normalize(code!);

        var student = await accounts.FindStudentRecoveryAsync(name!, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (student is null || student.LockoutEnd > now)
        {
            passwordHasher.SimulateVerification(given);
            throw Refused();
        }

        // Exactly one verification on every path, real or simulated: an account without a usable code
        // (none, expired, or a password set meanwhile) costs what a wrong code costs.
        var usableHash = student.PasswordFormat == PasswordFormat.None && student.RecoveryCodeExpires > now
            ? student.RecoveryCodeHash
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
                AccountRole.Student,
                student.Id,
                LoginPolicy.MaxFailedAttempts,
                now + LoginPolicy.LockoutDuration,
                cancellationToken).ConfigureAwait(false);
            throw Refused();
        }

        var completed = await accounts.CompleteStudentRecoveryAsync(student, now, passwordHasher.Hash(password!), cancellationToken).ConfigureAwait(false);
        if (!completed)
        {
            throw Refused();
        }

        audit.Record(AuditEvent.AccountRecovered, AccountRole.Student);
        return new UserSession(AccountRole.Student, student.Id, student.FullName);
    }

    public async Task<UserSession> RecoverTeacherAsync(
        string? setupKey,
        string? fullName,
        string? password,
        string? passwordRepeat,
        CancellationToken cancellationToken = default)
    {
        var name = InputRules.Clean(fullName);
        ValidateNewPassword(name, setupKey, password, passwordRepeat);
        return await RecoverWithSetupKeyAsync(AccountRole.Teacher, setupKey, name!, password!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The founder's recovery: the repository checks the key and uses it up in the transaction that
    /// writes the password, so a failed write leaves the key unused.
    /// </summary>
    private async Task<UserSession> RecoverWithSetupKeyAsync(AccountRole role, string? setupKey, string name, string password, CancellationToken cancellationToken)
    {
        var attempt = setupKeys.CreateAttempt(setupKey) ?? throw new BusinessRuleException(AuthService.InputRequired);
        var result = await accounts.SetFirstPasswordWithSetupKeyAsync(role, name, passwordHasher.Hash(password), attempt, cancellationToken).ConfigureAwait(false);
        switch (result.Status)
        {
            case SetupKeyWriteStatus.Done:
                audit.Record(AuditEvent.AccountRecovered, role);
                var displayName = role == AccountRole.Teacher
                    ? (await accounts.GetTeacherAsync(result.AccountId, cancellationToken).ConfigureAwait(false))?.FullName ?? name
                    : (await accounts.FindStudentRecoveryAsync(name, cancellationToken).ConfigureAwait(false))?.FullName ?? name;
                return new UserSession(role, result.AccountId, displayName);
            case SetupKeyWriteStatus.SetupCompleted:
                audit.Record(AuditEvent.RecoveryFailed, null);
                throw new BusinessRuleException(SetupService.SetupKeyNotAllowed);
            default:
                audit.Record(AuditEvent.RecoveryFailed, null);
                await setupKeys.DelayAfterRefusalAsync(cancellationToken).ConfigureAwait(false);
                throw new BusinessRuleException(role == AccountRole.Teacher ? TeacherRecoveryFailed : SetupKeyRecoveryFailed);
        }
    }

    private static void ValidateNewPassword(string? name, string? proof, string? password, string? passwordRepeat)
    {
        if (name is null || string.IsNullOrWhiteSpace(proof) || string.IsNullOrWhiteSpace(password) || proof.Length > 128)
        {
            throw new BusinessRuleException(AuthService.InputRequired);
        }

        if (name.Length > InputRules.PersonNameMaxLength)
        {
            throw new BusinessRuleException(AuthService.NameInvalid);
        }

        if (!string.Equals(password, passwordRepeat, StringComparison.Ordinal))
        {
            throw new BusinessRuleException(SetupService.PasswordsDoNotMatch);
        }

        if (password.Length is < PasswordPolicy.MinLength or > PasswordPolicy.MaxLength)
        {
            throw new BusinessRuleException(AuthService.PasswordInvalid);
        }
    }

    private BusinessRuleException Refused()
    {
        audit.Record(AuditEvent.RecoveryFailed, AccountRole.Student);
        return new BusinessRuleException(StudentRecoveryFailed);
    }
}
