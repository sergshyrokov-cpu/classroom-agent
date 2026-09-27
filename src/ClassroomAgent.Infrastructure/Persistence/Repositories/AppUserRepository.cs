using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The accounts of the installation (US-008 entity model §3.4). Stages changes only; the use case commits
/// through <see cref="IUnitOfWork"/> (AD-7, package-map).
/// </summary>
public sealed class AppUserRepository(ClassroomAgentDbContext db) : IAppUserRepository
{
    /// <summary>Tracked, because a successful sign-in updates the row it finds (db-design §3.4).</summary>
    public Task<AppUser?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.AppUsers.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

    /// <summary>Tracked: the caller rotates the stamp it reads (US-008 AC-014).</summary>
    public Task<AppUser?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
        db.AppUsers.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    /// <summary>Untracked: the session check only compares (US-008 security review F-4).</summary>
    public Task<AccountSessionState?> GetSessionStateAsync(long id, CancellationToken cancellationToken) =>
        db.AppUsers
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new AccountSessionState(u.SecurityStamp, u.IsDisabled))
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(AppUser user) => db.AppUsers.Add(user);

    public void Forget(AppUser user) => db.Entry(user).State = EntityState.Detached;

    /// <inheritdoc />
    public Task<AppUser?> FindDeanByIdAsync(long id, CancellationToken cancellationToken) =>
        db.AppUsers.SingleOrDefaultAsync(u => u.Id == id && u.Role == AppRole.Dean, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppUser>> ListDeansAsync(CancellationToken cancellationToken) =>
        await db.AppUsers
            .AsNoTracking()
            .Where(u => u.Role == AppRole.Dean)
            .OrderBy(u => u.NormalizedEmail)
            .ToListAsync(cancellationToken);
}
