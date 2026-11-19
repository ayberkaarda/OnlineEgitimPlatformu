using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Domain.Repositories;

/// <summary>
/// The administrators' account management. Nothing here returns a password, a hash or a code, and no
/// method can make an account an administrator: that is the database owner's decision
/// (docs/promote-admin.sql).
/// </summary>
public interface IAccountAdministrationRepository
{
    /// <summary>
    /// One page of the accounts that match <paramref name="criteria"/>, by name, then role and id, and
    /// the number of all matching accounts. The text matches names, e-mail addresses and specialties
    /// anywhere, literally.
    /// </summary>
    Task<AccountPage> SearchAsync(AccountSearchCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>Stores a new teacher (never an administrator) and returns its id.</summary>
    /// <exception cref="DuplicateAccountException">The name is taken by a teacher or an administrator.</exception>
    Task<int> AddTeacherAsync(Teacher teacher, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables or enables an account. Disabling increases its session version, so its sessions end.
    /// Under the administrators' lock, the last administrator who can sign in cannot be disabled.
    /// </summary>
    Task<AccountWriteStatus> SetDisabledAsync(AccountRole role, int id, bool disabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes an administrator a teacher again and ends its sessions. Under the administrators' lock, the
    /// last administrator who can sign in keeps the role.
    /// </summary>
    Task<AccountWriteStatus> DemoteAdminAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of an administrator's change to an account.</summary>
public enum AccountWriteStatus
{
    /// <summary>The change was stored.</summary>
    Done = 0,

    /// <summary>No such account (for a demotion: no such administrator); nothing was written.</summary>
    NotFound = 1,

    /// <summary>The change would leave no administrator who can sign in; nothing was written.</summary>
    LastActiveAdmin = 2,
}
