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

    public async Task<SetupStage> GetSetupStageAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (await db.Teachers.AnyAsync(t => t.PasswordFormat != PasswordFormat.None, cancellationToken).ConfigureAwait(false))
        {
            return SetupStage.Completed;
        }

        if (await db.Teachers.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return SetupStage.TeacherPasswordNeedsKey;
        }

        return await db.Students.AnyAsync(cancellationToken).ConfigureAwait(false)
            ? SetupStage.FirstTeacherNeedsKey
            : SetupStage.EmptyDatabase;
    }

    public async Task<FirstTeacherResult> AddFirstTeacherAsync(Teacher teacher, SetupKeyAttempt? setupKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(teacher);

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

            // An empty database is set up by whoever opens it first. Once students exist, the first
            // teacher needs the setup key, decided while the locks are held and used up in this
            // transaction: a failed write below rolls the use back.
            if (await AccountLocks.AnyStudentLockedAsync(db, cancellationToken).ConfigureAwait(false))
            {
                if (setupKey is null)
                {
                    return new FirstTeacherResult(FirstTeacherStatus.KeyRequired, 0);
                }

                if (!await UseSetupKeyAsync(db, setupKey, cancellationToken).ConfigureAwait(false))
                {
                    return new FirstTeacherResult(FirstTeacherStatus.NotAuthorized, 0);
                }
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

    public async Task<StudentRecoveryState?> FindStudentRecoveryAsync(string fullName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fullName);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Students
            .AsNoTracking()
            .Where(s => s.FullName == fullName)
            .Select(s => new StudentRecoveryState(s.Id, s.FullName, s.PasswordFormat, s.RecoveryCodeHash, s.RecoveryCodeExpires, s.LockoutEnd))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    // The teacher's ownership is part of the WHERE clause, so it is decided in the same statement that
    // writes: a student of another teacher, or of no course at all, matches no row.
    public async Task<bool> SetRecoveryCodeAsync(int teacherId, string studentName, string codeHash, DateTime expires, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(studentName);
        ArgumentNullException.ThrowIfNull(codeHash);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var updated = await db.Students
            .Where(s => s.FullName == studentName
                && s.PasswordFormat == PasswordFormat.None
                && db.Enrollments.Any(e => e.StudentId == s.Id
                    && db.Courses.Any(c => c.Id == e.CourseId && c.TeacherId == teacherId)))
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(a => a.RecoveryCodeHash, codeHash)
                    .SetProperty(a => a.RecoveryCodeExpires, expires),
                cancellationToken)
            .ConfigureAwait(false);
        return updated == 1;
    }

    // The WHERE clause is the final decision, as for a sign-in: a code replaced or used since it was
    // verified, a password set meanwhile, an expiry or a lock make the statement match no row.
    public async Task<bool> CompleteStudentRecoveryAsync(StudentRecoveryState verified, DateTime now, string newPasswordHash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verified);
        ArgumentNullException.ThrowIfNull(newPasswordHash);
        if (verified.RecoveryCodeHash is not { } codeHash)
        {
            return false;
        }

        var id = verified.Id;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var updated = await db.Students
            .Where(s => s.Id == id
                && s.PasswordFormat == PasswordFormat.None
                && s.RecoveryCodeHash == codeHash
                && s.RecoveryCodeExpires > now
                && (s.LockoutEnd == null || s.LockoutEnd <= now))
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(a => a.PasswordHash, newPasswordHash)
                    .SetProperty(a => a.PasswordFormat, PasswordFormat.Pbkdf2)
                    .SetProperty(a => a.RecoveryCodeHash, (string?)null)
                    .SetProperty(a => a.RecoveryCodeExpires, (DateTime?)null)
                    .SetProperty(a => a.FailedLoginCount, 0)
                    .SetProperty(a => a.LockoutEnd, (DateTime?)null),
                cancellationToken)
            .ConfigureAwait(false);
        return updated == 1;
    }

    public async Task<SetupKeyWriteResult> SetFirstPasswordWithSetupKeyAsync(
        AccountRole role,
        string fullName,
        string newPasswordHash,
        SetupKeyAttempt setupKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        ArgumentNullException.ThrowIfNull(newPasswordHash);
        ArgumentNullException.ThrowIfNull(setupKey);
        if (role is not (AccountRole.Student or AccountRole.Teacher))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role.");
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);

        // The teacher lock decides, until the commit, that no teacher can sign in with a password; a
        // concurrent recovery or setup waits here and then sees this one's result.
        await AccountLocks.AnyTeacherLockedAsync(db, cancellationToken).ConfigureAwait(false);
        if (await AnyTeacherWithPasswordAsync(db, cancellationToken).ConfigureAwait(false))
        {
            return new SetupKeyWriteResult(SetupKeyWriteStatus.SetupCompleted, 0);
        }

        // The key before the name: a wrong key is counted whatever name was given.
        if (!await UseSetupKeyAsync(db, setupKey, cancellationToken).ConfigureAwait(false))
        {
            return new SetupKeyWriteResult(SetupKeyWriteStatus.NotAuthorized, 0);
        }

        // Returning before the commit rolls the key's use back.
        var id = role == AccountRole.Teacher
            ? await SetFirstTeacherPasswordAsync(db, fullName, newPasswordHash, cancellationToken).ConfigureAwait(false)
            : await SetFirstStudentPasswordAsync(db, fullName, newPasswordHash, cancellationToken).ConfigureAwait(false);
        if (id is not { } accountId)
        {
            return new SetupKeyWriteResult(SetupKeyWriteStatus.NoSuchAccount, 0);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new SetupKeyWriteResult(SetupKeyWriteStatus.Done, accountId);
    }

    private static Task<bool> AnyTeacherWithPasswordAsync(EgitimDbContext db, CancellationToken cancellationToken) =>
        db.Teachers.AnyAsync(t => t.PasswordFormat != PasswordFormat.None, cancellationToken);

    /// <summary>
    /// Checks <paramref name="attempt"/> against the usable setup keys and, when one matches, uses it
    /// up with a conditional UPDATE of Tuketildi and TuketimTarihi (the only columns the application's
    /// login may change) inside the caller's transaction, so the use commits or rolls back with the
    /// caller's write. A wrong key writes nothing.
    /// </summary>
    private static async Task<bool> UseSetupKeyAsync(EgitimDbContext db, SetupKeyAttempt attempt, CancellationToken cancellationToken)
    {
        var keys = await AccountLocks.UsableSetupKeysLockedAsync(db, attempt.Now, cancellationToken).ConfigureAwait(false);
        if (attempt.FindMatch(keys) is not { } id)
        {
            return false;
        }

        // The WHERE clause is the final decision: of two transactions with the same key only one can
        // match the unused row.
        if (db.Database.IsSqlServer())
        {
            // The attempt's time was taken before the transaction waited for the teacher lock, so the
            // expiry is decided again here against the database's own clock, the one the issuing script
            // set SonKullanma with: a key that expired while this transaction waited matches no row.
            var used = await db.Database.ExecuteSqlAsync(
                $"""
                UPDATE dbo.KurulumAnahtarlari
                SET Tuketildi = 1, TuketimTarihi = SYSUTCDATETIME()
                WHERE AnahtarID = {id} AND Tuketildi = 0 AND SonKullanma > SYSUTCDATETIME()
                """,
                cancellationToken).ConfigureAwait(false);
            return used == 1;
        }

        // Other providers (the tests' SQLite) have no lock wait and use the attempt's clock.
        var now = attempt.Now;
        var consumed = await db.SetupKeys
            .Where(k => k.Id == id && !k.Consumed && k.ExpiresUtc > now)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(k => k.Consumed, true)
                    .SetProperty(k => k.ConsumedUtc, now),
                cancellationToken)
            .ConfigureAwait(false);
        return consumed == 1;
    }

    private static async Task<int?> SetFirstTeacherPasswordAsync(EgitimDbContext db, string fullName, string newPasswordHash, CancellationToken cancellationToken)
    {
        var id = await db.Teachers
            .Where(t => t.FullName == fullName && t.PasswordFormat == PasswordFormat.None)
            .Select(t => (int?)t.Id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (id is null)
        {
            return null;
        }

        var updated = await db.Teachers
            .Where(t => t.Id == id && t.PasswordFormat == PasswordFormat.None)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(a => a.PasswordHash, newPasswordHash)
                    .SetProperty(a => a.PasswordFormat, PasswordFormat.Pbkdf2)
                    .SetProperty(a => a.FailedLoginCount, 0)
                    .SetProperty(a => a.LockoutEnd, (DateTime?)null),
                cancellationToken)
            .ConfigureAwait(false);
        return updated == 1 ? id : null;
    }

    private static async Task<int?> SetFirstStudentPasswordAsync(EgitimDbContext db, string fullName, string newPasswordHash, CancellationToken cancellationToken)
    {
        var id = await db.Students
            .Where(s => s.FullName == fullName && s.PasswordFormat == PasswordFormat.None)
            .Select(s => (int?)s.Id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (id is null)
        {
            return null;
        }

        var updated = await db.Students
            .Where(s => s.Id == id && s.PasswordFormat == PasswordFormat.None)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(a => a.PasswordHash, newPasswordHash)
                    .SetProperty(a => a.PasswordFormat, PasswordFormat.Pbkdf2)
                    .SetProperty(a => a.RecoveryCodeHash, (string?)null)
                    .SetProperty(a => a.RecoveryCodeExpires, (DateTime?)null)
                    .SetProperty(a => a.FailedLoginCount, 0)
                    .SetProperty(a => a.LockoutEnd, (DateTime?)null),
                cancellationToken)
            .ConfigureAwait(false);
        return updated == 1 ? id : null;
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
