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

    void Add(AppUser user);

    /// <summary>Forgets what is tracked, so a unique-violation can be followed by a clean re-read (spec I-10).</summary>
    void Forget(AppUser user);
}
