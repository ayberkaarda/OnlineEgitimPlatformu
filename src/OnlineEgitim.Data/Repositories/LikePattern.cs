using System.Text;

namespace OnlineEgitim.Data.Repositories;

/// <summary>
/// LIKE patterns that match typed text literally. The text is passed as a parameter, never spliced into
/// the statement, and its wildcard characters are escaped: on SQL Server <c>%</c>, <c>_</c> and
/// <c>[</c> are special, on SQLite <c>%</c> and <c>_</c>; the escape character itself is escaped too.
/// </summary>
internal static class LikePattern
{
    /// <summary>The escape character given to LIKE ... ESCAPE.</summary>
    public const string Escape = "\\";

    /// <summary>
    /// "Contains <paramref name="text"/>" as a pattern, or <see langword="null"/> when there is nothing
    /// to match (null, empty or white space), so the caller adds no condition.
    /// </summary>
    public static string? Contains(string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        var builder = new StringBuilder(trimmed.Length + 8).Append('%');
        foreach (var c in trimmed)
        {
            if (c is '\\' or '%' or '_' or '[')
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }

        return builder.Append('%').ToString();
    }
}
