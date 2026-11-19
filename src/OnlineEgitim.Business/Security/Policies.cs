namespace OnlineEgitim.Business.Security;

/// <summary>Brute force protection: an account is locked for a while after repeated failures.</summary>
public static class LoginPolicy
{
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
}

/// <summary>Recovery of accounts that have no password.</summary>
public static class RecoveryPolicy
{
    /// <summary>How long a recovery code issued by a teacher works.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromHours(24);

    /// <summary>Characters of a recovery code (50 bits); wrong codes count towards the sign-in lockout.</summary>
    public const int CodeLength = 10;
}

/// <summary>Password length rules.</summary>
public static class PasswordPolicy
{
    /// <summary>Shortest password that can be chosen for a new account.</summary>
    public const int MinLength = 8;

    /// <summary>Longest password that can be chosen for a new account.</summary>
    public const int MaxLength = 64;

    /// <summary>
    /// Longest password accepted at sign-in. The original application stored passwords of any length
    /// up to its NVARCHAR(100) columns, so existing accounts may have passwords longer than
    /// <see cref="MaxLength"/>; sign-in must keep accepting them, before and after they are rehashed.
    /// </summary>
    public const int SignInMaxLength = 100;
}
