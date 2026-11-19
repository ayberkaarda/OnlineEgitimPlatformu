using OnlineEgitim.Business.Auditing;
using OnlineEgitim.Domain.Repositories;

namespace OnlineEgitim.Business.Services;

/// <summary>Decides, against the database, whether a session may still be used.</summary>
internal interface ISessionGuard
{
    /// <summary>
    /// Throws <see cref="SessionExpiredException"/> when the account of <paramref name="session"/> no
    /// longer exists, is disabled, has another role or another session version.
    /// </summary>
    Task EnsureCurrentAsync(UserSession session, CancellationToken cancellationToken);
}

internal sealed class SessionGuard(IAccountRepository accounts, IAuditLog audit) : ISessionGuard
{
    public async Task EnsureCurrentAsync(UserSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        var state = await accounts.GetSessionStateAsync(session.Role, session.UserId, cancellationToken).ConfigureAwait(false);
        if (state is null || state.Disabled || state.Role != session.Role || state.SessionVersion != session.SessionVersion)
        {
            audit.Record(AuditEvent.SessionRejected, session.Role);
            throw new SessionExpiredException();
        }
    }
}
