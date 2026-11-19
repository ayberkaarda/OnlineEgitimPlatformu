using System.Globalization;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.UI.Infrastructure;

/// <summary>How a list shows a course; the id travels with the item instead of being parsed back from the text.</summary>
internal enum CourseItemStyle
{
    /// <summary>"3 - Ad / Eğitmen / Konu", the student's course list.</summary>
    Catalog,

    /// <summary>"Ad / Eğitmen / Konu", the student's own courses.</summary>
    Mine,

    /// <summary>"3 - Ad (2/20 öğrenci)", the teacher's course list with its seats.</summary>
    Teaching,
}

/// <summary>A course in a list box.</summary>
internal sealed record CourseItem(CourseSummary Course, CourseItemStyle Style)
{
    public override string ToString() => Style switch
    {
        CourseItemStyle.Catalog => $"{Course.Id} - {Course.Name} / {Course.TeacherName} / {Topic}",
        CourseItemStyle.Mine => $"{Course.Name} / {Course.TeacherName} / {Topic}",
        _ => string.Create(CultureInfo.CurrentCulture, $"{Course.Id} - {Course.Name} ({Course.StudentCount}/{Seats} öğrenci)"),
    };

    private string Topic => string.IsNullOrWhiteSpace(Course.Topic) ? "-" : Course.Topic;

    private string Seats => Course.Capacity is { } capacity ? capacity.ToString(CultureInfo.CurrentCulture) : "sınırsız";
}
