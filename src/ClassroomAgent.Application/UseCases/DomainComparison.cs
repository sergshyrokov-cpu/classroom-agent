using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The one comparison rule behind BR-020 (US-009 spec FR-007): two domains are the same domain when they are
/// equal after trimming, lower-casing and removing one trailing dot. A subdomain is never the domain.
/// </summary>
/// <remarks>
/// Written once and used by the connection state, by both halves of the save check and by nothing else, so the
/// Owner's control cannot be enforced two subtly different ways. Internationalised domains are compared in
/// their ASCII form, which is what the Control Plane stores and what Google reports (spec I-4).
/// </remarks>
public static class DomainComparison
{
    /// <summary>Whether two domains are the same domain.</summary>
    public static bool AreSame(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && string.Equals(
            WorkspaceConnection.NormalizeDomain(left),
            WorkspaceConnection.NormalizeDomain(right),
            StringComparison.Ordinal);

    /// <summary>Whether an address belongs to that domain — its domain part, after the single <c>@</c>.</summary>
    public static bool BelongsTo(string? email, string? domain) =>
        !string.IsNullOrWhiteSpace(email) && AreSame(WorkspaceConnection.DomainOf(email), domain);
}
