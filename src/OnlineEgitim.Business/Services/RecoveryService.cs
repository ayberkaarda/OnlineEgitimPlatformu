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
/// a single use, expiring code for a student, and a teacher account gets its password with the setup key.
/// The account keeps its id and with it its courses and enrollments.
/// </summary>
public interface IRecoveryService
{
    /// <summary>
    /// Issues a recovery code for the student without a password who signs in with
    /// <paramref name="studentName"/>, replacing an earlier code. Teachers only.
    /// </summary>
    Task<RecoveryCode> IssueStudentCodeAsync(UserSession teacher, string? studentName, CancellationToken cancellationToken = default);

    /// <summary>Sets the first password of a student with the code a teacher issued and signs the student in.</summary>
    Task<UserSession> RecoverStudentAsync(string? fullName, string? code, string? password, string? passwordRepeat, CancellationToken cancellationToken = default);

    /// <summary>Sets the first password of a teacher without one with the setup key and signs the teacher in.</summary>
    Task<UserSession> RecoverTeacherAsync(string? setupKey, string? fullName, string? password, string? passwordRepeat, CancellationToken cancellationToken = default);
}

internal sealed class RecoveryService(
    IAccountRepository accounts,
    IPasswordHasher passwordHasher,
    ISetupKeyStore setupKeys,
    TimeProvider timeProvider,
    IAuditLog audit) : IRecoveryService
{
    internal const string StudentNameRequired = "Lütfen öğrencinin adını girin.";
    internal const string NoPasswordlessStudent = "Bu adla kayıtlı, şifresi olmayan bir öğrenci bulunamadı.";
    internal const string TeacherRecoveryFailed =
        "Şifre belirlenemedi. Kurulum anahtarını ve adı kontrol edin; yalnızca şifresi olmayan öğretmen hesaplarına şifre belirlenebilir.";

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

        var student = await accounts.FindStudentRecoveryAsync(name, cancellationToken).ConfigureAwait(false);
        if (student is null || student.PasswordFormat != PasswordFormat.None)
        {
            throw new BusinessRuleException(NoPasswordlessStudent);
        }

        var code = FileSetupKeyStore.NewCode(RecoveryPolicy.CodeLength);
        var expires = timeProvider.GetUtcNow().UtcDateTime + RecoveryPolicy.CodeLifetime;
        var stored = await accounts.SetRecoveryCodeAsync(student.Id, passwordHasher.Hash(FileSetupKeyStore.Normalize(code)), expires, cancellationToken).ConfigureAwait(false);
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
        var given = FileSetupKeyStore.Normalize(code!);

        var student = await accounts.FindStudentRecoveryAsync(name!, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (student is null || student.LockoutEnd > now)
        {
            passwordHasher.SimulateVerification(given);
            throw Refused();
        }

        var verified = student.PasswordFormat == PasswordFormat.None
            && student.RecoveryCodeHash is { } codeHash
            && student.RecoveryCodeExpires > now
            && passwordHasher.Verify(given, codeHash, PasswordFormat.Pbkdf2) != PasswordVerificationResult.Failed;
        if (!verified)
        {
            if (student.RecoveryCodeHash is null)
            {
                passwordHasher.SimulateVerification(given);
            }

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

        // The key is used up only when a teacher without a password was matched.
        var id = await accounts.SetPasswordOfPasswordlessTeacherAsync(
            name!,
            passwordHasher.Hash(password!),
            () => setupKeys.TryConsume(setupKey),
            cancellationToken).ConfigureAwait(false);
        var teacher = id is { } teacherId ? await accounts.GetTeacherAsync(teacherId, cancellationToken).ConfigureAwait(false) : null;
        if (teacher is null)
        {
            audit.Record(AuditEvent.RecoveryFailed, null);
            throw new BusinessRuleException(TeacherRecoveryFailed);
        }

        audit.Record(AuditEvent.AccountRecovered, AccountRole.Teacher);
        return new UserSession(AccountRole.Teacher, teacher.Id, teacher.FullName);
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
