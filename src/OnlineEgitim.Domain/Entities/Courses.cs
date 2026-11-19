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

    /// <summary>
    /// Most students the course takes at once, or <see langword="null"/> for no limit (every course of
    /// the original application). Zero closes the course to new enrollments.
    /// </summary>
    public int? Capacity { get; set; }

    /// <summary>
    /// UTC time the teacher deleted the course, or <see langword="null"/>. A deleted course is kept with
    /// its notes and its closed enrollments, but it is no longer listed, shown or joined.
    /// </summary>
    public DateTime? DeletedUtc { get; set; }
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
/// student can join a course only once, also under concurrency. Leaving keeps the row and sets
/// <see cref="LeftUtc"/>; joining again reopens the same row.
/// </summary>
public sealed class Enrollment
{
    public int StudentId { get; set; }

    public int CourseId { get; set; }

    /// <summary>
    /// UTC time the student left the course (or the course was deleted), or <see langword="null"/>
    /// while the enrollment is active. Only active enrollments count against the capacity and open
    /// the course's notes.
    /// </summary>
    public DateTime? LeftUtc { get; set; }
}
