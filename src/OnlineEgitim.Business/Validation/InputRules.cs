using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace OnlineEgitim.Business.Validation;

/// <summary>
/// The central input checks. The original forms only refused empty fields; the services now apply the
/// same checks plus length and shape limits, so no screen can skip them and no value can exceed its
/// column. Every method treats <see langword="null"/>, empty and white space only input alike.
/// </summary>
public static partial class InputRules
{
    /// <summary>Longest person name (column size of the original application).</summary>
    public const int PersonNameMaxLength = 100;

    public const int SpecialtyMaxLength = 100;

    public const int EmailMaxLength = 100;

    public const int CourseNameMaxLength = 100;

    public const int TopicMaxLength = 200;

    public const int NoteMaxLength = 500;

    /// <summary>
    /// Trims <paramref name="value"/>; returns <see langword="null"/> for null, empty or white space
    /// only input, so callers handle one "missing" case.
    /// </summary>
    public static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>Whether every value is present after <see cref="Clean"/>.</summary>
    public static bool AllPresent(params string?[] values) => values.All(v => Clean(v) is not null);

    /// <summary>
    /// A person's name for a new account: letters, apostrophes, hyphens and dots, words separated by
    /// single spaces, at most <see cref="PersonNameMaxLength"/> characters.
    /// </summary>
    public static bool IsValidPersonName([NotNullWhen(true)] string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= PersonNameMaxLength && PersonNamePattern().IsMatch(value);

    /// <summary>The usual e-mail shape: something@something.something, no spaces.</summary>
    public static bool IsValidEmail([NotNullWhen(true)] string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= EmailMaxLength && EmailPattern().IsMatch(value);

    /// <summary>Whether <paramref name="value"/> fits in <paramref name="maxLength"/> and has no control characters.</summary>
    public static bool IsValidText([NotNullWhen(true)] string? value, int maxLength) =>
        !string.IsNullOrEmpty(value) && value.Length <= maxLength && !value.Any(char.IsControl);

    /// <summary>Like <see cref="IsValidText"/>, but line breaks are allowed (multi-line notes).</summary>
    public static bool IsValidMultilineText([NotNullWhen(true)] string? value, int maxLength) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= maxLength
        && !value.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t');

    [GeneratedRegex(@"^[\p{L}][\p{L}'.\-]*( [\p{L}][\p{L}'.\-]*)*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PersonNamePattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmailPattern();
}
