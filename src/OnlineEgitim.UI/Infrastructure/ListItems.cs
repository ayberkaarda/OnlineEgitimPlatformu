using System.Globalization;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.UI.Infrastructure;

/// <summary>How a list shows a course; the id travels with the item instead of being parsed back from the text.</summary>
internal enum CourseItemStyle
{
    /// <summary>"3 - Ad / Eğitmen / Konu / 2/20 öğrenci", the student's course catalogue.</summary>
    Catalog,

    /// <summary>"Ad / Eğitmen / Konu", the student's own courses.</summary>
    Mine,

    /// <summary>"3 - Ad (2/20 öğrenci)", the teacher's course list with its seats.</summary>
    Teaching,
}

/// <summary>A course in a list box; <paramref name="Enrolled"/> marks the student's own courses in the catalogue.</summary>
internal sealed record CourseItem(CourseSummary Course, CourseItemStyle Style, bool Enrolled = false)
{
    public override string ToString() => Style switch
    {
        CourseItemStyle.Catalog => string.Create(
            CultureInfo.CurrentCulture,
            $"{Course.Id} - {Course.Name} / {Course.TeacherName} / {Topic} / {Course.StudentCount}/{Seats} öğrenci{(Enrolled ? " / Kayıtlısınız" : string.Empty)}"),
        CourseItemStyle.Mine => $"{Course.Name} / {Course.TeacherName} / {Topic}",
        _ => string.Create(CultureInfo.CurrentCulture, $"{Course.Id} - {Course.Name} ({Course.StudentCount}/{Seats} öğrenci)"),
    };

    private string Topic => string.IsNullOrWhiteSpace(Course.Topic) ? "-" : Course.Topic;

    private string Seats => Course.Capacity is { } capacity ? capacity.ToString(CultureInfo.CurrentCulture) : "sınırsız";
}

/// <summary>An account in the administrators' list: "[Öğretmen] Ad - Uzmanlık (devre dışı, şifresiz)".</summary>
internal sealed record AccountItem(AccountListing Account)
{
    public override string ToString()
    {
        var notes = new List<string>(2);
        if (Account.Disabled)
        {
            notes.Add("devre dışı");
        }

        if (!Account.HasPassword)
        {
            notes.Add("şifresiz");
        }

        var suffix = notes.Count > 0 ? " (" + string.Join(", ", notes) + ")" : string.Empty;
        return $"[{RoleName(Account.Role)}] {Account.FullName} - {Account.Detail}{suffix}";
    }

    internal static string RoleName(AccountRole role) => role switch
    {
        AccountRole.Student => "Öğrenci",
        AccountRole.Teacher => "Öğretmen",
        AccountRole.Admin => "Yönetici",
        _ => "?",
    };
}
