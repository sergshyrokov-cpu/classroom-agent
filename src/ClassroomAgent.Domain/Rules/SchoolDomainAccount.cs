namespace ClassroomAgent.Domain.Rules;

/// <summary>
/// US-031 spec FR-006, OD-005: an email is a domain account's when the part after its last <c>@</c> equals the school's
/// domain exactly, compared case-insensitively (ordinal, invariant); a subdomain is not. Shared by the organizer test of
/// FR-005 and the participant test of FR-008.
/// </summary>
public static class SchoolDomainAccount
{
    public static bool IsDomainAccount(string? email, string schoolDomain)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(schoolDomain))
        {
            return false;
        }

        var at = email.LastIndexOf('@');
        return at >= 0 && string.Equals(email[(at + 1)..], schoolDomain, StringComparison.OrdinalIgnoreCase);
    }
}
