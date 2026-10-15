using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The real account repository with a hook after each read by login, so a test can land another write on the row
/// between a sign-in's read and its save — a controlled interleaving instead of a timing race (US-048 spec §9). The
/// hook receives the 1-based number of the read; the reads are counted so a test can assert how many runs the
/// sequence made.
/// </summary>
public sealed class InterleavingAppUserRepository(IAppUserRepository inner, Func<int, Task> afterReadByEmail)
    : IAppUserRepository
{
    /// <summary>How many times the account was read by login.</summary>
    public int ReadsByEmail { get; private set; }

    public async Task<AppUser?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var account = await inner.FindByNormalizedEmailAsync(normalizedEmail, cancellationToken);
        ReadsByEmail++;
        await afterReadByEmail(ReadsByEmail);
        return account;
    }

    public Task<AppUser?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
        inner.FindByIdAsync(id, cancellationToken);

    public Task<AccountSessionState?> GetSessionStateAsync(long id, CancellationToken cancellationToken) =>
        inner.GetSessionStateAsync(id, cancellationToken);

    public Task<AppUser?> FindDeanByIdAsync(long id, CancellationToken cancellationToken) =>
        inner.FindDeanByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<AppUser>> ListDeansAsync(CancellationToken cancellationToken) =>
        inner.ListDeansAsync(cancellationToken);

    public void Add(AppUser user) => inner.Add(user);

    public void Forget(AppUser user) => inner.Forget(user);
}
