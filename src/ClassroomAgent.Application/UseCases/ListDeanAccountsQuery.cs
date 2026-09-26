using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Every Dean account, active and disabled alike, for the Admin's screen (US-012 spec FR-011). Reading is never
/// blocked in read-only mode (BR-026), so this query consults no guard.
/// </summary>
/// <remarks>US-012 TEST_WRITING skeleton (OD-005) — IMPLEMENTATION writes the body.</remarks>
public sealed class ListDeanAccountsQuery
{
    /// <summary>US-012 TEST_WRITING skeleton (OD-005): IMPLEMENTATION turns this into a primary constructor
    /// holding the dependencies. They are listed here so the tests construct the type exactly as it will be.</summary>
    public ListDeanAccountsQuery(
        IAppUserRepository users)
    {
    }

    public Task<IReadOnlyList<DeanAccountRow>> ExecuteAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException("US-012 IMPLEMENTATION (spec FR-011).");
}
