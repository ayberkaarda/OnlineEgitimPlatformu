using Microsoft.EntityFrameworkCore;
using OnlineEgitim.Data.Persistence;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Data.Repositories;

internal sealed class AccountAdministrationRepository(IDbContextFactory<EgitimDbContext> contextFactory) : IAccountAdministrationRepository
{
    private const int AdminLockTimeoutMilliseconds = 30_000;

    /// <summary>Serializes every change that can lower the number of administrators who can sign in.</summary>
    private const string AdminLock = "OnlineEgitim.Administrators";

    public async Task<AccountPage> SearchAsync(AccountSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentOutOfRangeException.ThrowIfNegative(criteria.Skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(criteria.Take);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var pattern = LikePattern.Contains(criteria.Text);

        var students = db.Students.AsNoTracking()
            .Where(s => pattern == null || EF.Functions.Like(s.FullName, pattern, LikePattern.Escape) || EF.Functions.Like(s.Email, pattern, LikePattern.Escape))
            .Select(s => new AccountRow
            {
                Role = AccountRole.Student,
                Id = s.Id,
                FullName = s.FullName,
                Detail = s.Email,
                Disabled = s.Disabled,
                HasPassword = s.PasswordFormat != PasswordFormat.None,
            });
        var staff = db.Teachers.AsNoTracking()
            .Where(t => pattern == null || EF.Functions.Like(t.FullName, pattern, LikePattern.Escape) || EF.Functions.Like(t.Specialty, pattern, LikePattern.Escape))
            .Select(t => new AccountRow
            {
                Role = t.Role,
                Id = t.Id,
                FullName = t.FullName,
                Detail = t.Specialty,
                Disabled = t.Disabled,
                HasPassword = t.PasswordFormat != PasswordFormat.None,
            });

        var rows = criteria.Role switch
        {
            AccountRole.Student => students,
            AccountRole.Teacher or AccountRole.Admin => staff.Where(r => r.Role == criteria.Role),
            _ => students.Concat(staff),
        };

        var total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);

        // Name, then role and id: a student and a teacher of the same name keep their order on every page.
        var page = await rows
            .OrderBy(r => r.FullName)
            .ThenBy(r => r.Role)
            .ThenBy(r => r.Id)
            .Skip(criteria.Skip)
            .Take(criteria.Take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new AccountPage(page.Select(r => new AccountListing(r.Role, r.Id, r.FullName, r.Detail, r.Disabled, r.HasPassword)).ToList(), total);
    }

    public async Task<int> AddTeacherAsync(Teacher teacher, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(teacher);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = new Teacher
        {
            FullName = teacher.FullName,
            Specialty = teacher.Specialty,
            PasswordHash = teacher.PasswordHash,
            PasswordFormat = teacher.PasswordFormat,
            RecoveryCodeHash = teacher.RecoveryCodeHash,
            RecoveryCodeExpires = teacher.RecoveryCodeExpires,
            Role = AccountRole.Teacher,
        };
        db.Teachers.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            await using var check = await contextFactory.CreateDbContextAsync(CancellationToken.None).ConfigureAwait(false);
            if (await check.Teachers.AnyAsync(t => t.FullName == teacher.FullName, CancellationToken.None).ConfigureAwait(false))
            {
                throw new DuplicateAccountException("The teacher name is already registered.", ex);
            }

            throw;
        }

        return entity.Id;
    }

    public async Task<AccountWriteStatus> SetDisabledAsync(AccountRole role, int id, bool disabled, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (role == AccountRole.Student)
        {
            // Disabling ends the account's sessions; enabling leaves the version as it is.
            var students = db.Students.Where(s => s.Id == id);
            var changed = disabled
                ? await students.ExecuteUpdateAsync(s => s.SetProperty(a => a.Disabled, true).SetProperty(a => a.SessionVersion, a => a.SessionVersion + 1), cancellationToken).ConfigureAwait(false)
                : await students.ExecuteUpdateAsync(s => s.SetProperty(a => a.Disabled, false), cancellationToken).ConfigureAwait(false);
            return changed == 1 ? AccountWriteStatus.Done : AccountWriteStatus.NotFound;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await AppLock.AcquireAsync(db, AdminLock, AdminLockTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);

        var target = await db.Teachers.Where(t => t.Id == id && (t.Role == AccountRole.Teacher || t.Role == AccountRole.Admin))
            .Select(t => new { t.Role, t.Disabled, t.PasswordFormat })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (target is null || (role == AccountRole.Admin) != (target.Role == AccountRole.Admin))
        {
            return AccountWriteStatus.NotFound;
        }

        if (disabled && IsActiveAdmin(target.Role, target.Disabled, target.PasswordFormat)
            && !await OtherActiveAdminExistsAsync(db, id, cancellationToken).ConfigureAwait(false))
        {
            return AccountWriteStatus.LastActiveAdmin;
        }

        var staff = db.Teachers.Where(t => t.Id == id);
        _ = disabled
            ? await staff.ExecuteUpdateAsync(s => s.SetProperty(a => a.Disabled, true).SetProperty(a => a.SessionVersion, a => a.SessionVersion + 1), cancellationToken).ConfigureAwait(false)
            : await staff.ExecuteUpdateAsync(s => s.SetProperty(a => a.Disabled, false), cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return AccountWriteStatus.Done;
    }

    public async Task<AccountWriteStatus> DemoteAdminAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await AppLock.AcquireAsync(db, AdminLock, AdminLockTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);

        var target = await db.Teachers.Where(t => t.Id == id && t.Role == AccountRole.Admin)
            .Select(t => new { t.Role, t.Disabled, t.PasswordFormat })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (target is null)
        {
            return AccountWriteStatus.NotFound;
        }

        if (IsActiveAdmin(target.Role, target.Disabled, target.PasswordFormat)
            && !await OtherActiveAdminExistsAsync(db, id, cancellationToken).ConfigureAwait(false))
        {
            return AccountWriteStatus.LastActiveAdmin;
        }

        // Lowering the role is the only role change the application's login may make; the database
        // trigger refuses the other direction for anyone but the database owner.
        await db.Teachers
            .Where(t => t.Id == id && t.Role == AccountRole.Admin)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Role, AccountRole.Teacher).SetProperty(a => a.SessionVersion, a => a.SessionVersion + 1), cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return AccountWriteStatus.Done;
    }

    /// <summary>An administrator who can sign in: not disabled and with a password.</summary>
    private static bool IsActiveAdmin(AccountRole role, bool disabled, PasswordFormat format) =>
        role == AccountRole.Admin && !disabled && format != PasswordFormat.None;

    private static Task<bool> OtherActiveAdminExistsAsync(EgitimDbContext db, int id, CancellationToken cancellationToken) =>
        db.Teachers.AnyAsync(
            t => t.Id != id && t.Role == AccountRole.Admin && !t.Disabled && t.PasswordFormat != PasswordFormat.None,
            cancellationToken);

    /// <summary>One row of the account list; a class with settable members so EF can sort the union of both tables.</summary>
    private sealed class AccountRow
    {
        public AccountRole Role { get; init; }

        public int Id { get; init; }

        public string FullName { get; init; } = string.Empty;

        public string Detail { get; init; } = string.Empty;

        public bool Disabled { get; init; }

        public bool HasPassword { get; init; }
    }
}
