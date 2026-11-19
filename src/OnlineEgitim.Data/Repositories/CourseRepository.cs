using Microsoft.EntityFrameworkCore;
using OnlineEgitim.Data.Persistence;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Data.Repositories;

internal sealed class CourseRepository(IDbContextFactory<EgitimDbContext> contextFactory) : ICourseRepository
{
    public async Task<IReadOnlyList<CourseSummary>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await Summaries(db, db.Courses).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CourseSummary>> GetByTeacherAsync(int teacherId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await Summaries(db, db.Courses.Where(c => c.TeacherId == teacherId)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CourseSummary?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await Summaries(db, db.Courses.Where(c => c.Id == id)).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> AddAsync(Course course, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(course);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = new Course { Name = course.Name, Topic = course.Topic, TeacherId = course.TeacherId };
        db.Courses.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task<IReadOnlyList<string>> GetNotesAsync(int courseId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Notes
            .AsNoTracking()
            .Where(n => n.CourseId == courseId)
            .OrderBy(n => n.Id)
            .Select(n => n.Content)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> AddNoteAsync(int courseId, int teacherId, string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        if (!await db.Courses.AnyAsync(c => c.Id == courseId && c.TeacherId == teacherId, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        db.Notes.Add(new CourseNote { CourseId = courseId, Content = content });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static IQueryable<CourseSummary> Summaries(EgitimDbContext db, IQueryable<Course> courses) =>
        from c in courses.AsNoTracking()
        join t in db.Teachers on c.TeacherId equals t.Id
        orderby c.Id
        select new CourseSummary(c.Id, c.Name, c.TeacherId, t.FullName, c.Topic);
}
