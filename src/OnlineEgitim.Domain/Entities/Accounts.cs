namespace OnlineEgitim.Domain.Entities;

/// <summary>
/// The kinds of account. Students live in their own table (Ogrenci); teachers and administrators share
/// the staff table (Egitmenler), told apart by its Rol column.
/// </summary>
public enum AccountRole
{
    /// <summary>Joins courses and sees only their own courses and the notes of those courses (table: Ogrenci).</summary>
    Student = 0,

    /// <summary>Creates courses, adds notes to them and sees who joined them (table: Egitmenler, Rol = 1).</summary>
    Teacher = 1,

    /// <summary>
    /// Manages accounts: creates teachers and students, disables and enables them and issues reset codes
    /// (table: Egitmenler, Rol = 2). An administrator does not teach.
    /// </summary>
    Admin = 2,
}

/// <summary>
/// How an account's stored password value has to be checked (column SifreBicimi). It is recorded
/// explicitly instead of being read from the value, because the original application let users choose
/// any text as a password, including text that looks like a hash.
/// </summary>
public enum PasswordFormat
{
    /// <summary>A PBKDF2 hash written by this application. Also the default for new accounts.</summary>
    Pbkdf2 = 0,

    /// <summary>
    /// The plain text password kept from a database of the original application. Replaced, together
    /// with the format, by a hash on the account's next successful sign-in.
    /// </summary>
    LegacyPlainText = 1,

    /// <summary>
    /// The account has no usable password: the original application stored none (NULL or empty), or an
    /// administrator created the account with a single use code instead of a password. Sign-in is
    /// refused until a password is set.
    /// </summary>
    None = 2,
}

/// <summary>Sign-in state shared by every account kind.</summary>
public abstract class Account
{
    public int Id { get; set; }

    /// <summary>The name the account signs in with; unique within its table.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// PBKDF2 hash of the password. Rows created by the original application hold the plain text
    /// password until the first successful sign-in replaces it; <see cref="PasswordFormat"/> tells which
    /// of the two it is.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    public PasswordFormat PasswordFormat { get; set; }

    /// <summary>Failed sign-in attempts since the last success or lockout.</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>UTC time until which sign-in is refused, or <see langword="null"/>.</summary>
    public DateTime? LockoutEnd { get; set; }

    /// <summary>An administrator disabled the account: it cannot sign in and its sessions end.</summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Increased whenever the account's sessions have to end (a new password, a disabled account, a
    /// changed role). A session carries the value it was issued with and is refused once they differ.
    /// </summary>
    public int SessionVersion { get; set; }

    /// <summary>
    /// PBKDF2 hash of the single use code an authorized person issued to set a new password, or
    /// <see langword="null"/>. The code itself is never stored.
    /// </summary>
    public string? RecoveryCodeHash { get; set; }

    /// <summary>UTC time after which <see cref="RecoveryCodeHash"/> no longer works.</summary>
    public DateTime? RecoveryCodeExpires { get; set; }
}

/// <summary>A student (table: Ogrenci).</summary>
public sealed class Student : Account
{
    /// <summary>E-mail address; unique.</summary>
    public string Email { get; set; } = string.Empty;
}

/// <summary>A teacher or an administrator (table: Egitmenler).</summary>
public sealed class Teacher : Account
{
    /// <summary>Field of expertise, shown with the teacher's courses; an administrator's duty.</summary>
    public string Specialty { get; set; } = string.Empty;

    /// <summary><see cref="AccountRole.Teacher"/> or <see cref="AccountRole.Admin"/>.</summary>
    public AccountRole Role { get; set; } = AccountRole.Teacher;
}
