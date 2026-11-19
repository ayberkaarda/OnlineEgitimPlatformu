using Microsoft.EntityFrameworkCore;
using OnlineEgitim.Data.Persistence;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Data.Repositories;

internal sealed class EnrollmentRepository(IDbContextFactory<EgitimDbContext> contextFactory) : IEnrollmentRepository
{
    public async Task<EnrollmentStatus> AddAsync(int studentId, int courseId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // The course's lock first: every write that depends on the course's seats takes it, so the
        // count below stays true until the commit.
        await CourseLock.AcquireAsync(db, courseId, cancellationToken).ConfigureAwait(false);

        var course = await db.Courses
            .Where(c => c.Id == courseId && c.DeletedUtc == null)
            .Select(c => new { c.Capacity })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (course is null)
        {
            return EnrollmentStatus.CourseNotFound;
        }

        if (await db.Enrollments.AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId, cancellationToken).ConfigureAwait(false))
        {
            return EnrollmentStatus.AlreadyEnrolled;
        }

        var students = await db.Enrollments.CountAsync(e => e.CourseId == courseId && e.LeftUtc == null, cancellationToken).ConfigureAwait(false);
        if (course.Capacity is { } capacity && students >= capacity)
        {
            return EnrollmentStatus.CourseFull;
        }

        db.Enrollments.Add(new Enrollment { StudentId = studentId, CourseId = courseId });
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException) when (!db.Database.IsSqlServer())
        {
            // Without the lock (SQLite) the primary key decides a concurrent duplicate.
            return EnrollmentStatus.AlreadyEnrolled;
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return EnrollmentStatus.Enrolled;
    }

    public async Task<bool> IsEnrolledAsync(int studentId, int courseId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ActiveOfCourse(db, courseId).AnyAsync(e => e.StudentId == studentId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CourseSummary>> GetCoursesOfStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var joined = db.Courses.Where(c => db.Enrollments.Any(e => e.CourseId == c.Id && e.StudentId == studentId && e.LeftUtc == null));
        return await CourseRepository.Summaries(db, joined).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetStudentNamesAsync(int courseId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await (
                from e in ActiveOfCourse(db, courseId).AsNoTracking()
                join s in db.Students on e.StudentId equals s.Id
                orderby s.FullName, s.Id
                select s.FullName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountStudentsAsync(int courseId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ActiveOfCourse(db, courseId).CountAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Active enrollments of the course, if the course is not deleted.</summary>
    private static IQueryable<Enrollment> ActiveOfCourse(EgitimDbContext db, int courseId) =>
        db.Enrollments.Where(e => e.CourseId == courseId
            && e.LeftUtc == null
            && db.Courses.Any(c => c.Id == courseId && c.DeletedUtc == null));
}
