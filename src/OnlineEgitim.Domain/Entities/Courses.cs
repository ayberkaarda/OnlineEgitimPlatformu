namespace OnlineEgitim.Domain.Entities;

/// <summary>A course given by one teacher (table: Dersler).</summary>
public sealed class Course
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>The teacher who owns the course; only that teacher sees its students and adds notes.</summary>
    public int TeacherId { get; set; }

    /// <summary>Subject of the course; may be missing on rows of the original application.</summary>
    public string? Topic { get; set; }
}

/// <summary>A note a teacher shared with the students of a course (table: Notlar).</summary>
public sealed class CourseNote
{
    public int Id { get; set; }

    public int CourseId { get; set; }

    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// A student's membership in a course (table: OgrenciKurslar). The pair is the primary key, so a
/// student can join a course only once, also under concurrency.
/// </summary>
public sealed class Enrollment
{
    public int StudentId { get; set; }

    public int CourseId { get; set; }
}
