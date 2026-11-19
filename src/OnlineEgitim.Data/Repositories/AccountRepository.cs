using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using OnlineEgitim.Data.Persistence;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Data.Repositories;

internal sealed class AccountRepository(IDbContextFactory<EgitimDbContext> contextFactory) : IAccountRepository
{
    public async Task<AccountCredentials?> FindAsync(AccountRole role, string fullName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fullName);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return role switch
        {
            AccountRole.Student => await FindAsync(db.Students, fullName, cancellationToken).ConfigureAwait(false),
            AccountRole.Teacher => await FindAsync(db.Teachers, fullName, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role."),
        };
    }

    public async Task RecordFailedLoginAsync(
        AccountRole role,
        int id,
        int maxAttempts,
        DateTime lockoutEnd,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        _ = role switch
        {
            AccountRole.Student => await RecordFailedLoginAsync(db.Students, id, maxAttempts, lockoutEnd, cancellationToken).ConfigureAwait(false),
            AccountRole.Teacher => await RecordFailedLoginAsync(db.Teachers, id, maxAttempts, lockoutEnd, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role."),
        };
    }

    public async Task<bool> RecordSuccessfulLoginAsync(
        AccountRole role,
        AccountCredentials verified,
        DateTime now,
        string? newPasswordHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verified);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var updated = role switch
        {
            AccountRole.Student => await RecordSuccessfulLoginAsync(db.Students, verified, now, newPasswordHash, cancellationToken).ConfigureAwait(false),
            AccountRole.Teacher => await RecordSuccessfulLoginAsync(db.Teachers, verified, now, newPasswordHash, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role."),
        };
        return updated == 1;
    }

    public async Task<bool> StudentExistsAsync(string fullName, string email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        ArgumentNullException.ThrowIfNull(email);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Students.AnyAsync(s => s.FullName == fullName || s.Email == email, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> AddStudentAsync(Student student, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(student);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = new Student
        {
            FullName = student.FullName,
            Email = student.Email,
            PasswordHash = student.PasswordHash,
            PasswordFormat = student.PasswordFormat,
        };
        db.Students.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // A unique index rejected a concurrent registration of the same name or e-mail address.
            // Checked with a new query so the provider specific error code does not matter.
            if (await StudentExistsAsync(student.FullName, student.Email, CancellationToken.None).ConfigureAwait(false))
            {
                throw new DuplicateAccountException("The student name or e-mail address is already registered.", ex);
            }

            throw;
        }

        return entity.Id;
    }

    public async Task<bool> AnyTeacherAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Teachers.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> AnyAccountAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Teachers.AnyAsync(cancellationToken).ConfigureAwait(false)
            || await db.Students.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<FirstTeacherResult> AddFirstTeacherAsync(Teacher teacher, Func<bool> authorize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(teacher);
        ArgumentNullException.ThrowIfNull(authorize);

        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            // Serializable: the emptiness check holds a range lock until the commit, so two concurrent
            // setups can never both insert. On SQL Server the check takes an update lock, which a second
            // setup has to wait for, so the loser sees the winner's row instead of becoming a deadlock victim.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
            if (await AccountLocks.AnyTeacherLockedAsync(db, cancellationToken).ConfigureAwait(false))
            {
                return new FirstTeacherResult(FirstTeacherStatus.TeacherExists, 0);
            }

            // Decided while the lock is held: only the setup that gets here first can use the authority.
            if (!authorize())
            {
                return new FirstTeacherResult(FirstTeacherStatus.NotAuthorized, 0);
            }

            var entity = new Teacher
            {
                FullName = teacher.FullName,
                Specialty = teacher.Specialty,
                PasswordHash = teacher.PasswordHash,
                PasswordFormat = teacher.PasswordFormat,
            };
            db.Teachers.Add(entity);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new FirstTeacherResult(FirstTeacherStatus.Created, entity.Id);
        }
        catch (Exception ex) when (ex is DbUpdateException or DbException || ex.InnerException is DbUpdateException or DbException)
        {
            // Lost the race (deadlock victim or a rejected insert); the winner's row decides.
            if (await AnyTeacherAsync(CancellationToken.None).ConfigureAwait(false))
            {
                return new FirstTeacherResult(FirstTeacherStatus.TeacherExists, 0);
            }

            throw;
        }
    }

    public async Task<TeacherProfile?> GetTeacherAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Teachers
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TeacherProfile(t.Id, t.FullName, t.Specialty))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static Task<AccountCredentials?> FindAsync<TAccount>(DbSet<TAccount> accounts, string fullName, CancellationToken cancellationToken)
        where TAccount : Account =>
        accounts
            .AsNoTracking()
            .Where(a => a.FullName == fullName)
            .Select(a => new AccountCredentials(a.Id, a.FullName, a.PasswordHash, a.PasswordFormat, a.FailedLoginCount, a.LockoutEnd))
            .SingleOrDefaultAsync(cancellationToken);

    // One UPDATE statement, so concurrent failures cannot lose increments.
    private static Task<int> RecordFailedLoginAsync<TAccount>(
        DbSet<TAccount> accounts,
        int id,
        int maxAttempts,
        DateTime lockoutEnd,
        CancellationToken cancellationToken)
        where TAccount : Account =>
        accounts
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(a => a.LockoutEnd, a => a.FailedLoginCount + 1 >= maxAttempts ? lockoutEnd : a.LockoutEnd)
                    .SetProperty(a => a.FailedLoginCount, a => a.FailedLoginCount + 1 >= maxAttempts ? 0 : a.FailedLoginCount + 1),
                cancellationToken);

    // The WHERE clause is the final decision: a lock set by a concurrent failure, or a password
    // changed since it was verified, makes the statement match no row and the sign-in fails.
    private static Task<int> RecordSuccessfulLoginAsync<TAccount>(
        DbSet<TAccount> accounts,
        AccountCredentials verified,
        DateTime now,
        string? newPasswordHash,
        CancellationToken cancellationToken)
        where TAccount : Account
    {
        var id = verified.Id;
        var verifiedHash = verified.PasswordHash;
        var verifiedFormat = verified.PasswordFormat;
        var hash = newPasswordHash ?? verifiedHash;
        var format = newPasswordHash is null ? verifiedFormat : PasswordFormat.Pbkdf2;

        return accounts
            .Where(a => a.Id == id
                && (a.LockoutEnd == null || a.LockoutEnd <= now)
                && a.PasswordHash == verifiedHash
                && a.PasswordFormat == verifiedFormat)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(a => a.FailedLoginCount, 0)
                    .SetProperty(a => a.LockoutEnd, (DateTime?)null)
                    .SetProperty(a => a.PasswordHash, hash)
                    .SetProperty(a => a.PasswordFormat, format),
                cancellationToken);
    }
}
