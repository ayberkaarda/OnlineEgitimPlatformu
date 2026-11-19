namespace OnlineEgitim.Domain.Entities;

/// <summary>
/// How far the setup of the database is. It decides who may create the first teacher and whether a
/// setup key is accepted: only while no teacher can sign in with a password.
/// </summary>
public enum SetupStage
{
    /// <summary>No account at all: the first teacher is created without a setup key.</summary>
    EmptyDatabase = 0,

    /// <summary>Students but no teacher: the first teacher needs a setup key.</summary>
    FirstTeacherNeedsKey = 1,

    /// <summary>
    /// Teachers exist, but none can sign in with a password (accounts the original application left
    /// without one): a setup key sets the password of one of them.
    /// </summary>
    TeacherPasswordNeedsKey = 2,

    /// <summary>A teacher signs in with a password: no setup key is accepted.</summary>
    Completed = 3,
}

/// <summary>
/// A single use setup key (table: KurulumAnahtarlari). Keys are created only by the database owner
/// with docs/issue-setup-key.sql, which shows the key once and stores its SHA-256 hash. The
/// application's login may read the rows and mark a key used (<see cref="Consumed"/>,
/// <see cref="ConsumedUtc"/>), nothing else: it can neither create, replace nor extend a key.
/// </summary>
public sealed class SetupKey
{
    public int Id { get; set; }

    /// <summary>SHA-256 of the normalized key (32 upper case hexadecimal characters, ASCII).</summary>
    public byte[] KeyHash { get; set; } = [];

    public DateTime CreatedUtc { get; set; }

    public DateTime ExpiresUtc { get; set; }

    /// <summary>Set in the same transaction as the account write the key authorized; also set by the issuing script to revoke a key.</summary>
    public bool Consumed { get; set; }

    public DateTime? ConsumedUtc { get; set; }
}
