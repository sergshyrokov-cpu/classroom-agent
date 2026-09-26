using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The installation's accounts (US-008 entity model §3.4). Stages changes; never saves — the use case owns the
/// transaction boundary through <see cref="IUnitOfWork"/> (AD-7).
/// </summary>
public interface IAppUserRepository
{
    /// <summary>The account with that normalized email, tracked for an update; null when none exists.</summary>
    Task<AppUser?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>The account with that id, tracked for an update; null when none exists (US-008 AC-014).</summary>
    Task<AppUser?> FindByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// The security stamp and disabled flag of that account, read untracked; null when no such account exists.
    /// The per-request session check needs nothing else, and reading it untracked keeps a later commit in the same
    /// request from flushing an entity nobody meant to change (US-008 security review F-4).
    /// </summary>
    Task<AccountSessionState?> GetSessionStateAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// The account with that id **only when its role is Dean**, tracked for an update; null otherwise, so an
    /// Admin account and an id that matches nothing are one answer (US-012 spec VR-005, I-8).
    /// </summary>
    Task<AppUser?> FindDeanByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Every account with role Dean, active and disabled alike, ordered by email, read untracked for the
    /// Admin's screen (US-012 spec FR-011). Unpaginated by OD-003.
    /// </summary>
    Task<IReadOnlyList<AppUser>> ListDeansAsync(CancellationToken cancellationToken);

    void Add(AppUser user);

    /// <summary>Forgets what is tracked, so a unique-violation can be followed by a clean re-read (spec I-10).</summary>
    void Forget(AppUser user);
}
