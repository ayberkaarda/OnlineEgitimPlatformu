using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.UI.Infrastructure;

/// <summary>How a list shows a course; the id travels with the item instead of being parsed back from the text.</summary>
internal enum CourseItemStyle
{
    /// <summary>"3 - Ad / Eğitmen / Konu", the student's course list.</summary>
    Catalog,

    /// <summary>"Ad / Eğitmen / Konu", the student's own courses.</summary>
    Mine,

    /// <summary>"3 - Ad", the teacher's course list.</summary>
    Teaching,
}

/// <summary>A course in a list box.</summary>
internal sealed record CourseItem(CourseSummary Course, CourseItemStyle Style)
{
    public override string ToString() => Style switch
    {
        CourseItemStyle.Catalog => $"{Course.Id} - {Course.Name} / {Course.TeacherName} / {Topic}",
        CourseItemStyle.Mine => $"{Course.Name} / {Course.TeacherName} / {Topic}",
        _ => $"{Course.Id} - {Course.Name}",
    };

    private string Topic => string.IsNullOrWhiteSpace(Course.Topic) ? "-" : Course.Topic;
}
