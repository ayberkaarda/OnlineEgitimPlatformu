using Microsoft.EntityFrameworkCore;
using OnlineEgitim.Data.Persistence;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Data.Repositories;

internal sealed class EnrollmentRepository(IDbContextFactory<EgitimDbContext> contextFactory) : IEnrollmentRepository
{
    public async Task AddAsync(int studentId, int courseId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.Enrollments.Add(new Enrollment { StudentId = studentId, CourseId = courseId });

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // The primary key rejected a second (possibly concurrent) enrollment. Checked with a new
            // query so the provider specific error code does not matter.
            if (await IsEnrolledAsync(studentId, courseId, CancellationToken.None).ConfigureAwait(false))
            {
                throw new AlreadyEnrolledException("The student already joined the course.", ex);
            }

            throw;
        }
    }

    public async Task<bool> IsEnrolledAsync(int studentId, int courseId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Enrollments.AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CourseSummary>> GetCoursesOfStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await (
                from e in db.Enrollments.AsNoTracking()
                join c in db.Courses on e.CourseId equals c.Id
                join t in db.Teachers on c.TeacherId equals t.Id
                where e.StudentId == studentId
                orderby c.Id
                select new CourseSummary(c.Id, c.Name, c.TeacherId, t.FullName, c.Topic))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetStudentNamesAsync(int courseId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await (
                from e in db.Enrollments.AsNoTracking()
                join s in db.Students on e.StudentId equals s.Id
                where e.CourseId == courseId
                orderby s.FullName, s.Id
                select s.FullName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountStudentsAsync(int courseId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Enrollments.CountAsync(e => e.CourseId == courseId, cancellationToken).ConfigureAwait(false);
    }
}
