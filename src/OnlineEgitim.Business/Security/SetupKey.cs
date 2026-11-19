using System.Security.Cryptography;
using System.Text;

namespace OnlineEgitim.Business.Security;

/// <summary>
/// The founder's authority: a local, single use key. Whoever can read it on the computer that runs the
/// application may create the first teacher or set the password of a teacher account that has none.
/// Having no teacher in the database is not such an authority.
/// </summary>
public interface ISetupKeyStore
{
    /// <summary>
    /// File that holds the key, shown to the installer, or <see langword="null"/> when the key comes
    /// from the <see cref="FileSetupKeyStore.EnvironmentVariable"/> environment variable.
    /// </summary>
    string? KeyFilePath { get; }

    /// <summary>Writes a new random key to <see cref="KeyFilePath"/> unless an unused key exists.</summary>
    void EnsureIssued();

    /// <summary>
    /// Checks <paramref name="candidate"/> and, when it is the current key, uses it up in the same
    /// step: of any number of calls with the right key, also concurrent ones, exactly one returns
    /// <see langword="true"/>. A wrong key changes nothing.
    /// </summary>
    bool TryConsume(string? candidate);
}

/// <summary>
/// Keeps the setup key in a file of the application's local data folder (readable by the user who
/// installed the application only), or takes it from <see cref="EnvironmentVariable"/>. A used file key
/// is deleted; a used environment key is recorded by its hash, so it stays invalid until another value
/// is set. The key itself is never logged.
/// </summary>
public sealed class FileSetupKeyStore : ISetupKeyStore
{
    /// <summary>Environment variable with a setup key chosen by the operator (at least <see cref="MinimumEnvironmentKeyLength"/> characters).</summary>
    public const string EnvironmentVariable = "EGITIM_SETUP_KEY";

    public const int MinimumEnvironmentKeyLength = 16;

    internal const string KeyFileName = "setup-key.txt";

    // Crockford base32 without the easily confused I, L, O and U: 20 characters carry 100 bits.
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int KeyLength = 20;

    private readonly string _directory;
    private readonly string? _environmentKey;

    public FileSetupKeyStore(string directory, string? environmentKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        _directory = directory;
        _environmentKey = environmentKey is { Length: >= MinimumEnvironmentKeyLength } ? Normalize(environmentKey) : null;
        KeyFilePath = _environmentKey is null ? Path.Combine(directory, KeyFileName) : null;
    }

    public string? KeyFilePath { get; }

    /// <summary>The key folder is %LOCALAPPDATA%\OnlineEgitim; the environment variable takes precedence.</summary>
    public static FileSetupKeyStore CreateDefault() =>
        new(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OnlineEgitim"),
            Environment.GetEnvironmentVariable(EnvironmentVariable));

    public void EnsureIssued()
    {
        if (KeyFilePath is null)
        {
            return;
        }

        Directory.CreateDirectory(_directory);
        try
        {
            // OpenOrCreate under an exclusive lock: an unused key, also one written concurrently, is
            // kept; an emptied (used) file gets a new key.
            using var stream = new FileStream(KeyFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            {
                if (Normalize(reader.ReadToEnd()).Length > 0)
                {
                    return;
                }
            }

            stream.SetLength(0);
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.WriteLine(NewCode(KeyLength));
        }
        catch (IOException) when (File.Exists(KeyFilePath))
        {
            // Being written or consumed by another instance right now.
        }
    }

    public bool TryConsume(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var given = Normalize(candidate);
        return _environmentKey is not null ? TryConsumeEnvironmentKey(given) : TryConsumeFileKey(given);
    }

    /// <summary>Upper case, without spaces and dashes, so the key can be typed as it is shown.</summary>
    internal static string Normalize(string key)
    {
        var builder = new StringBuilder(key.Length);
        foreach (var c in key)
        {
            if (c is not ('-' or ' ') && !char.IsWhiteSpace(c))
            {
                builder.Append(char.ToUpperInvariant(c));
            }
        }

        return builder.ToString();
    }

    /// <summary>A random code of <paramref name="length"/> base32 characters in groups of five.</summary>
    internal static string NewCode(int length)
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

    private static bool SameKey(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    /// <summary>
    /// Reads and empties the key file under an exclusive lock, so of concurrent consumers only the
    /// first one with the right key finds it; the emptied file is then deleted.
    /// </summary>
    private bool TryConsumeFileKey(string given)
    {
        const int Attempts = 20;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using (var stream = new FileStream(KeyFilePath!, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    string stored;
                    using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
                    {
                        stored = Normalize(reader.ReadToEnd());
                    }

                    if (stored.Length == 0 || !SameKey(stored, given))
                    {
                        return false;
                    }

                    stream.SetLength(0);
                    stream.Flush(flushToDisk: true);
                }

                TryDelete(KeyFilePath!);
                return true;
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException) when (attempt < Attempts)
            {
                // Another consumer holds the file for a moment.
                Thread.Sleep(10);
            }
            catch (IOException)
            {
                return false;
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An emptied key file is no key; EnsureIssued replaces it.
        }
    }

    private bool TryConsumeEnvironmentKey(string given)
    {
        if (!SameKey(_environmentKey!, given))
        {
            return false;
        }

        // The marker is named by the key's hash; creating it fails when it exists, so a value of the
        // environment variable can be used once.
        Directory.CreateDirectory(_directory);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_environmentKey!)));
        var marker = Path.Combine(_directory, "setup-key-used-" + hash);
        try
        {
            using var stream = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
