using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Every Dean account, active and disabled alike, for the Admin's screen (US-012 spec FR-011). Reading is never
/// blocked in read-only mode (BR-026), so this query consults no guard.
/// </summary>
public sealed class ListDeanAccountsQuery(IAppUserRepository users)
{
    public async Task<IReadOnlyList<DeanAccountRow>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var deans = await users.ListDeansAsync(cancellationToken);
        return deans
            .Select(d => new DeanAccountRow(
                d.Id,
                d.Email,
                d.IsDisabled,
                d.PasswordIsTemporary,
                d.LastSuccessfulSignInAt))
            .ToList();
    }
}
