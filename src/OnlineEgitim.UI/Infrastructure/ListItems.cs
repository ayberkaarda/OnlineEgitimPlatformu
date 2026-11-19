using System.Globalization;
using OnlineEgitim.Domain.Entities;
using OnlineEgitim.UI.Theming;

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

/// <summary>
/// A course in a list box; <paramref name="Enrolled"/> marks the student's own courses in the catalogue. The
/// text (<see cref="ToString"/>) is what screen readers and copying use; <see cref="Row"/> is how the themed
/// list draws it.
/// </summary>
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

    /// <summary>
    /// The drawn row: the course name first; the seats lead the second line and the status pill (own enrollment,
    /// full, free seats) sits beside the name, so a long name or teacher never hides them.
    /// </summary>
    public ListRow Row() => Style switch
    {
        CourseItemStyle.Catalog => new ListRow(
            Course.Name,
            Join(Occupancy, Course.TeacherName, Course.Topic),
            RowTone.Neutral,
            Enrolled ? "Kayıtlısınız" : SeatStatus,
            Enrolled ? RowTone.Accent : SeatTone),
        CourseItemStyle.Mine => new ListRow(Course.Name, Join(Course.TeacherName, Course.Topic)),
        _ => new ListRow(Course.Name, Join(Occupancy, Course.Topic), RowTone.Neutral, SeatStatus, SeatTone),
    };

    /// <summary>"24/30 öğrenci", or "37 öğrenci" for a course without a limit.</summary>
    private string Occupancy => Course.Capacity is { } capacity
        ? string.Create(CultureInfo.CurrentCulture, $"{Course.StudentCount}/{capacity} öğrenci")
        : string.Create(CultureInfo.CurrentCulture, $"{Course.StudentCount} öğrenci");

    private string SeatStatus => Course.Capacity switch
    {
        null => "Sınırsız",
        _ when !Course.HasFreeSeat => "Dolu",
        { } capacity => string.Create(CultureInfo.CurrentCulture, $"{capacity - Course.StudentCount} boş yer"),
    };

    private RowTone SeatTone => Course.Capacity is null ? RowTone.Neutral : Course.HasFreeSeat ? RowTone.Warm : RowTone.Warning;

    private string Topic => string.IsNullOrWhiteSpace(Course.Topic) ? "-" : Course.Topic;

    private string Seats => Course.Capacity is { } capacity ? capacity.ToString(CultureInfo.CurrentCulture) : "sınırsız";

    private static string Join(params string?[] parts) => string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}

/// <summary>An account in the administrators' list: "[Öğretmen] Ad - Uzmanlık (devre dışı, şifresiz)".</summary>
internal sealed record AccountItem(AccountListing Account)
{
    public override string ToString()
    {
        var notes = Notes();
        var suffix = notes.Count > 0 ? " (" + string.Join(", ", notes) + ")" : string.Empty;
        return $"[{RoleName(Account.Role)}] {Account.FullName} - {Account.Detail}{suffix}";
    }

    /// <summary>The name first, the role and detail below it and the account's state as a pill beside the name.</summary>
    public ListRow Row()
    {
        var notes = Notes();
        var status = notes.Count > 0
            ? string.Join(", ", notes)
            : Account.Role == AccountRole.Admin ? "Yönetici" : null;
        var tone = Account.Disabled ? RowTone.Warning : notes.Count > 0 ? RowTone.Warm : RowTone.Accent;
        return new ListRow(Account.FullName, RoleName(Account.Role) + " · " + Account.Detail, RowTone.Neutral, Capitalize(status), tone);
    }

    internal static string RoleName(AccountRole role) => role switch
    {
        AccountRole.Student => "Öğrenci",
        AccountRole.Teacher => "Öğretmen",
        AccountRole.Admin => "Yönetici",
        _ => "?",
    };

    private List<string> Notes()
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

        return notes;
    }

    private static string? Capitalize(string? text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0], CultureInfo.GetCultureInfo("tr-TR")) + text[1..];
}
