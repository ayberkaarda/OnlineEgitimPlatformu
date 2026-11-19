namespace OnlineEgitim.Data.Persistence;

/// <summary>
/// Column sizes and value rules enforced by the database. The business layer applies the same or
/// stricter rules; the legacy upgrade checks existing rows against these values.
/// </summary>
internal static class SchemaLimits
{
    /// <summary>Sizes of the original columns (NVARCHAR(100), NVARCHAR(200) and NVARCHAR(500)).</summary>
    public const int PersonNameLength = 100;
    public const int SpecialtyLength = 100;
    public const int EmailLength = 100;
    public const int CourseNameLength = 100;
    public const int TopicLength = 200;
    public const int NoteLength = 500;

    /// <summary>Room for a PBKDF2 hash; the original columns held plain text in 100 characters.</summary>
    public const int PasswordLength = 256;

    /// <summary>A SHA-256 hash of a setup key (BINARY(32)).</summary>
    public const int SetupKeyHashLength = 32;

    // Plain literals: the same text has to work on SQLite, which has no N'' prefix.
    public const string FailedLoginCheck = "[BasarisizGiris] >= 0";

    /// <summary>Values of <see cref="Domain.Entities.PasswordFormat"/>: 0 PBKDF2 hash, 1 legacy plain text, 2 none.</summary>
    public const string PasswordFormatCheck = "[SifreBicimi] IN (0, 1, 2)";

    /// <summary>A course's capacity: no limit (NULL) or a number of seats from zero up.</summary>
    public const string CapacityCheck = "[Kontenjan] IS NULL OR [Kontenjan] >= 0";
}
