using System.Security.Cryptography;
using System.Text;

namespace OnlineEgitim.Business.Security;

/// <summary>
/// Random codes as people type them: Crockford base32 without the easily confused I, L, O and U, five
/// bits per character, shown in groups of five. Recovery codes use it; setup keys share its
/// normalization.
/// </summary>
internal static class CodeFormat
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Upper case, without spaces and dashes, so a code can be typed as it is shown.</summary>
    public static string Normalize(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var builder = new StringBuilder(code.Length);
        foreach (var c in code)
        {
            if (c is not ('-' or ' ') && !char.IsWhiteSpace(c))
            {
                builder.Append(char.ToUpperInvariant(c));
            }
        }

        return builder.ToString();
    }

    /// <summary>A random code of <paramref name="length"/> characters in groups of five.</summary>
    public static string NewCode(int length)
    {
        var builder = new StringBuilder(length + (length / 5));
        for (var i = 0; i < length; i++)
        {
            if (i > 0 && i % 5 == 0)
            {
                builder.Append('-');
            }

            builder.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        }

        return builder.ToString();
    }
}
