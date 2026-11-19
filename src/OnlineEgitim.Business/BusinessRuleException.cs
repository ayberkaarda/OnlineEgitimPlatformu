namespace OnlineEgitim.Business;

/// <summary>
/// Raised when an operation violates a business rule. The message is user facing (Turkish), fixed
/// and free of entered values, so it can be shown to the user and written to a log as is.
/// </summary>
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException()
    {
    }

    public BusinessRuleException(string message)
        : base(message)
    {
    }

    public BusinessRuleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Raised when a session is no longer valid: the account was disabled, its password was reset or
/// changed, or its role changed since the session was issued. The user has to sign in again.
/// </summary>
public sealed class SessionExpiredException : Exception
{
    internal const string DefaultMessage = "Oturumunuz sona erdi. Lütfen yeniden giriş yapın.";

    public SessionExpiredException()
        : base(DefaultMessage)
    {
    }

    public SessionExpiredException(string message)
        : base(message)
    {
    }

    public SessionExpiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Raised when the signed-in account tries to see or change data that does not belong to it. The
/// message is fixed and does not reveal whether the requested record exists.
/// </summary>
public sealed class AccessDeniedException : Exception
{
    internal const string DefaultMessage = "Bu işlem için yetkiniz yok.";

    public AccessDeniedException()
        : base(DefaultMessage)
    {
    }

    public AccessDeniedException(string message)
        : base(message)
    {
    }

    public AccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
