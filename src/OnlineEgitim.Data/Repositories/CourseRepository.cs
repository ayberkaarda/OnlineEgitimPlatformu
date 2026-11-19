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

    public async Task<CatalogPage> SearchAsync(CourseSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentOutOfRangeException.ThrowIfNegative(criteria.Skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(criteria.Take);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var studentId = criteria.StudentId;
        var rows =
            from c in db.Courses.AsNoTracking()
            where c.DeletedUtc == null
            join t in db.Teachers on c.TeacherId equals t.Id
            select new
            {
                Course = c,
                TeacherName = t.FullName,
                Students = db.Enrollments.Count(e => e.CourseId == c.Id && e.LeftUtc == null),
                Enrolled = db.Enrollments.Any(e => e.CourseId == c.Id && e.StudentId == studentId && e.LeftUtc == null),
            };

        if (LikePattern.Contains(criteria.Name) is { } name)
        {
            rows = rows.Where(r => EF.Functions.Like(r.Course.Name, name, LikePattern.Escape));
        }

        if (LikePattern.Contains(criteria.Topic) is { } topic)
        {
            rows = rows.Where(r => r.Course.Topic != null && EF.Functions.Like(r.Course.Topic, topic, LikePattern.Escape));
        }

        if (LikePattern.Contains(criteria.TeacherName) is { } teacher)
        {
            rows = rows.Where(r => EF.Functions.Like(r.TeacherName, teacher, LikePattern.Escape));
        }

        if (criteria.OnlyWithFreeSeats)
        {
            rows = rows.Where(r => r.Course.Capacity == null || r.Students < r.Course.Capacity);
        }

        rows = criteria.Enrollment switch
        {
            EnrollmentFilter.Enrolled => rows.Where(r => r.Enrolled),
            EnrollmentFilter.NotEnrolled => rows.Where(r => !r.Enrolled),
            _ => rows,
        };

        var total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);

        // Name and then id: courses with the same name keep their order from page to page.
        var page = await rows
            .OrderBy(r => r.Course.Name)
            .ThenBy(r => r.Course.Id)
            .Skip(criteria.Skip)
            .Take(criteria.Take)
            .Select(r => new CatalogEntry(
                new CourseSummary(r.Course.Id, r.Course.Name, r.Course.TeacherId, r.TeacherName, r.Course.Topic, r.Course.Capacity, r.Students),
                r.Enrolled))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new CatalogPage(page, total);
    }

    public async Task<int> AddAsync(Course course, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(course);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = new Course { Name = course.Name, Topic = course.Topic, TeacherId = course.TeacherId, Capacity = course.Capacity };
        db.Courses.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task<CourseWriteResult> UpdateAsync(
        int courseId,
        int teacherId,
        string name,
        string? topic,
        int? capacity,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await CourseLock.AcquireAsync(db, courseId, cancellationToken).ConfigureAwait(false);

        if (!await OwnedCourse(db, courseId, teacherId).AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return new CourseWriteResult(CourseWriteStatus.NotFound, 0);
        }

        // Counted under the lock every enrollment takes, so no student can join between this count and
        // the commit: a capacity below the students who are in the course is never stored.
        var students = await ActiveEnrollments(db, courseId).CountAsync(cancellationToken).ConfigureAwait(false);
        if (capacity < students)
        {
            return new CourseWriteResult(CourseWriteStatus.CapacityBelowEnrollments, students);
        }

        await OwnedCourse(db, courseId, teacherId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(c => c.Name, name)
                    .SetProperty(c => c.Topic, topic)
                    .SetProperty(c => c.Capacity, capacity),
                cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new CourseWriteResult(CourseWriteStatus.Done, students);
    }

    public async Task<CourseWriteResult> DeleteAsync(
        int courseId,
        int teacherId,
        int expectedStudents,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await CourseLock.AcquireAsync(db, courseId, cancellationToken).ConfigureAwait(false);

        if (!await OwnedCourse(db, courseId, teacherId).AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return new CourseWriteResult(CourseWriteStatus.NotFound, 0);
        }

        // The teacher confirmed the deletion for this many students; a student who joined or left
        // since then changes what the teacher agreed to, so the deletion has to be confirmed again.
        var students = await ActiveEnrollments(db, courseId).CountAsync(cancellationToken).ConfigureAwait(false);
        if (students != expectedStudents)
        {
            return new CourseWriteResult(CourseWriteStatus.StudentsChanged, students);
        }

        await ActiveEnrollments(db, courseId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.LeftUtc, now), cancellationToken)
            .ConfigureAwait(false);
        await OwnedCourse(db, courseId, teacherId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.DeletedUtc, now), cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new CourseWriteResult(CourseWriteStatus.Done, students);
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

        if (!await OwnedCourse(db, courseId, teacherId).AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        db.Notes.Add(new CourseNote { CourseId = courseId, Content = content });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>The course with the id, if it is not deleted and belongs to the teacher.</summary>
    private static IQueryable<Course> OwnedCourse(EgitimDbContext db, int courseId, int teacherId) =>
        db.Courses.Where(c => c.Id == courseId && c.TeacherId == teacherId && c.DeletedUtc == null);

    private static IQueryable<Enrollment> ActiveEnrollments(EgitimDbContext db, int courseId) =>
        db.Enrollments.Where(e => e.CourseId == courseId && e.LeftUtc == null);

    /// <summary>Courses that are not deleted, with their teacher and their number of active enrollments.</summary>
    internal static IQueryable<CourseSummary> Summaries(EgitimDbContext db, IQueryable<Course> courses) =>
        from c in courses.AsNoTracking()
        where c.DeletedUtc == null
        join t in db.Teachers on c.TeacherId equals t.Id
        orderby c.Id
        select new CourseSummary(
            c.Id,
            c.Name,
            c.TeacherId,
            t.FullName,
            c.Topic,
            c.Capacity,
            db.Enrollments.Count(e => e.CourseId == c.Id && e.LeftUtc == null));
}
