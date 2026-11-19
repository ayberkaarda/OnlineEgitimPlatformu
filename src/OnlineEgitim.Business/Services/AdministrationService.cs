using FluentValidation;
using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Validation;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Services;

/// <summary>An administrator's account search; pages are counted from 0.</summary>
public sealed record AccountSearch(string? Text = null, AccountRole? Role = null, int PageIndex = 0, int PageSize = AccountSearch.DefaultPageSize)
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 50;
}

/// <summary>One page of an account search; <see cref="PageIndex"/> is the page actually returned.</summary>
public sealed record AccountSearchResult(IReadOnlyList<AccountListing> Items, int TotalCount, int PageIndex, int PageSize)
{
    /// <summary>Number of pages, at least one.</summary>
    public int PageCount => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
}

/// <summary>
/// Account management, for administrators only; every method checks the role and the session first.
/// New accounts get no password: the administrator hands over a single use code, shown once, with which
/// the person sets a password of their own. No method shows or reads a password, and none makes an
/// account an administrator: that is the database owner's decision (docs/promote-admin.sql).
/// </summary>
public interface IAdministrationService
{
    /// <summary>One page of the accounts whose name, e-mail address or specialty contains the text.</summary>
    Task<AccountSearchResult> SearchAccountsAsync(UserSession admin, AccountSearch search, CancellationToken cancellationToken = default);

    /// <summary>Creates a teacher account without a password and returns its single use code.</summary>
    Task<RecoveryCode> CreateTeacherAsync(UserSession admin, string? fullName, string? specialty, CancellationToken cancellationToken = default);

    /// <summary>Creates a student account without a password and returns its single use code.</summary>
    Task<RecoveryCode> CreateStudentAsync(UserSession admin, string? fullName, string? email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables or enables an account; a disabled account cannot sign in and its sessions end. The last
    /// administrator who can sign in cannot be disabled.
    /// </summary>
    Task SetDisabledAsync(UserSession admin, AccountRole role, int accountId, bool disabled, CancellationToken cancellationToken = default);

    /// <summary>Makes an administrator a teacher. The last administrator who can sign in keeps the role.</summary>
    Task DemoteAdminAsync(UserSession admin, int accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a single use reset code (24 hours) for any account, replacing an earlier one. The person
    /// sets a new password with it; the old password works until then, and every session of the account
    /// ends when the code is used. The code is returned once and never stored or logged.
    /// </summary>
    Task<RecoveryCode> IssueResetCodeAsync(UserSession admin, AccountRole role, int accountId, CancellationToken cancellationToken = default);
}

internal sealed class AdministrationService(
    IAccountRepository accounts,
    IAccountAdministrationRepository administration,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider,
    IAuditLog audit,
    ISessionGuard sessions,
    IValidator<TeacherAccountInput> teacherValidator,
    IValidator<StudentAccountInput> studentValidator,
    IValidator<AccountSearch> searchValidator) : IAdministrationService
{
    internal const string AccountNotFound = "Hesap bulunamadı.";
    internal const string TeacherNameTaken = "Bu adla kayıtlı bir öğretmen veya yönetici var.";
    internal const string StudentTaken = "Bu ad veya e-posta adresiyle kayıtlı bir öğrenci var.";
    internal const string LastActiveAdmin =
        "Son etkin yönetici devre dışı bırakılamaz ve yöneticilikten çıkarılamaz. Önce başka bir yöneticinin giriş yapabildiğinden emin olun.";

    public async Task<AccountSearchResult> SearchAccountsAsync(UserSession admin, AccountSearch search, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(admin, cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(search);
        searchValidator.ValidateOrThrow(search);

        var pageIndex = search.PageIndex;
        var page = await administration.SearchAsync(Criteria(search, pageIndex), cancellationToken).ConfigureAwait(false);
        if (page.Items.Count == 0 && pageIndex > 0)
        {
            pageIndex = page.TotalCount == 0 ? 0 : (page.TotalCount - 1) / search.PageSize;
            if (page.TotalCount > 0)
            {
                page = await administration.SearchAsync(Criteria(search, pageIndex), cancellationToken).ConfigureAwait(false);
            }
        }

        return new AccountSearchResult(page.Items, page.TotalCount, pageIndex, search.PageSize);
    }

    public async Task<RecoveryCode> CreateTeacherAsync(UserSession admin, string? fullName, string? specialty, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(admin, cancellationToken).ConfigureAwait(false);
        var input = TeacherAccountInput.Create(fullName, specialty);
        teacherValidator.ValidateOrThrow(input);

        var (code, hash, expires) = NewCode();
        try
        {
            await administration.AddTeacherAsync(
                new Teacher
                {
                    FullName = input.FullName!,
                    Specialty = input.Specialty!,
                    PasswordFormat = PasswordFormat.None,
                    RecoveryCodeHash = hash,
                    RecoveryCodeExpires = expires,
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateAccountException ex)
        {
            throw new BusinessRuleException(TeacherNameTaken, ex);
        }

        audit.Record(AuditEvent.AccountCreated, AccountRole.Teacher);
        return new RecoveryCode(code, expires);
    }

    public async Task<RecoveryCode> CreateStudentAsync(UserSession admin, string? fullName, string? email, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(admin, cancellationToken).ConfigureAwait(false);
        var input = StudentAccountInput.Create(fullName, email);
        studentValidator.ValidateOrThrow(input);

        // An administrator is entitled to know that a name or address is taken; the public
        // registration answers this case like a new registration instead.
        if (await accounts.StudentExistsAsync(input.FullName!, input.Email!, cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleException(StudentTaken);
        }

        var (code, hash, expires) = NewCode();
        try
        {
            await accounts.AddStudentAsync(
                new Student
                {
                    FullName = input.FullName!,
                    Email = input.Email!,
                    PasswordFormat = PasswordFormat.None,
                    RecoveryCodeHash = hash,
                    RecoveryCodeExpires = expires,
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateAccountException ex)
        {
            throw new BusinessRuleException(StudentTaken, ex);
        }

        audit.Record(AuditEvent.AccountCreated, AccountRole.Student);
        return new RecoveryCode(code, expires);
    }

    public async Task SetDisabledAsync(UserSession admin, AccountRole role, int accountId, bool disabled, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(admin, cancellationToken).ConfigureAwait(false);
        if (!Enum.IsDefined(role) || accountId <= 0)
        {
            throw new BusinessRuleException(AccountNotFound);
        }

        var status = await administration.SetDisabledAsync(role, accountId, disabled, cancellationToken).ConfigureAwait(false);
        Complete(status, disabled ? AuditEvent.AccountDisabled : AuditEvent.AccountEnabled, role);
    }

    public async Task DemoteAdminAsync(UserSession admin, int accountId, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(admin, cancellationToken).ConfigureAwait(false);
        if (accountId <= 0)
        {
            throw new BusinessRuleException(AccountNotFound);
        }

        var status = await administration.DemoteAdminAsync(accountId, cancellationToken).ConfigureAwait(false);
        Complete(status, AuditEvent.AdminDemoted, AccountRole.Admin);
    }

    public async Task<RecoveryCode> IssueResetCodeAsync(UserSession admin, AccountRole role, int accountId, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(admin, cancellationToken).ConfigureAwait(false);
        if (!Enum.IsDefined(role) || accountId <= 0)
        {
            throw new BusinessRuleException(AccountNotFound);
        }

        var (code, hash, expires) = NewCode();
        var status = await administration.SetRecoveryCodeAsync(role, accountId, hash, expires, cancellationToken).ConfigureAwait(false);
        if (status != AccountWriteStatus.Done)
        {
            throw new BusinessRuleException(AccountNotFound);
        }

        audit.Record(AuditEvent.RecoveryCodeIssued, AccountRole.Admin);
        return new RecoveryCode(code, expires);
    }

    private void Complete(AccountWriteStatus status, AuditEvent done, AccountRole role)
    {
        switch (status)
        {
            case AccountWriteStatus.Done:
                audit.Record(done, role);
                return;
            case AccountWriteStatus.LastActiveAdmin:
                throw new BusinessRuleException(LastActiveAdmin);
            default:
                throw new BusinessRuleException(AccountNotFound);
        }
    }

    /// <summary>The role first (no database access for a wrong role), then the session against the database.</summary>
    private async Task RequireAdminAsync(UserSession admin, CancellationToken cancellationToken)
    {
        Access.RequireRole(admin, AccountRole.Admin, audit);
        await sessions.EnsureCurrentAsync(admin, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A new single use code with its stored hash and expiry; the code itself is returned once and never stored or logged.</summary>
    private (string Code, string Hash, DateTime Expires) NewCode()
    {
        var code = CodeFormat.NewCode(RecoveryPolicy.CodeLength);
        return (code, passwordHasher.Hash(CodeFormat.Normalize(code)), timeProvider.GetUtcNow().UtcDateTime + RecoveryPolicy.CodeLifetime);
    }

    private static AccountSearchCriteria Criteria(AccountSearch search, int pageIndex) =>
        new(InputRules.Clean(search.Text), search.Role, pageIndex * search.PageSize, search.PageSize);
}
