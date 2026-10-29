using ClassroomAgent.Application.Validation;
using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The address rules of a Dean's login, shared by creation (US-012 spec VR-001, FR-004) and the login change
/// (US-067 spec FR-002 steps 3–4), so the two cannot drift apart. Both callers pass the trimmed address.
/// </summary>
internal static class DeanLoginAddress
{
    /// <summary>
    /// The address shape of US-012 spec VR-001 — the same rule US-009 applies to the technical account: at most 254
    /// characters, no whitespace, exactly one <c>@</c>, a non-empty local part and a domain-shaped domain part.
    /// </summary>
    public static bool IsWellFormed(string address)
    {
        if (address.Length is 0 or > ServiceAccountEmailAttribute.MaximumLength || address.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var parts = address.Split('@');
        return parts.Length == 2
            && parts[0].Length > 0
            && WorkspaceDomainAttribute.IsDomain(WorkspaceConnection.NormalizeDomain(parts[1]));
    }

    /// <summary>
    /// The school's domain as the legitimacy state holds it, read server-side and never taken from a form; null when
    /// none is known, which refuses every address (US-012 spec FR-004, S-04).
    /// </summary>
    public static string? SchoolDomainOf(LegitimacyState? state) =>
        string.IsNullOrEmpty(state?.Domain) ? null : state.Domain;

    /// <summary>
    /// The address is in the school's domain when the two domains are equal. A subdomain of the school's domain
    /// is not the school's domain (US-012 spec FR-004). The address must be well formed.
    /// </summary>
    public static bool IsInDomain(string address, string? schoolDomain) =>
        schoolDomain is not null
        && string.Equals(
            WorkspaceConnection.NormalizeDomain(address.Split('@')[1]),
            WorkspaceConnection.NormalizeDomain(schoolDomain),
            StringComparison.Ordinal);
}
