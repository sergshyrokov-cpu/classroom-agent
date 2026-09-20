namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// The installation's connection to one Google Workspace domain (US-009 entity model §2.1;
/// <c>trebovaniya.md</c> §3): the domain and the school's technical account, which is the impersonation user
/// the service account reads data on behalf of (BR-015). One record per installation — there is one active
/// domain at a time.
/// </summary>
/// <remarks>
/// <b>Neither the service-account key nor any reference to it belongs here</b>: the Owner places both at
/// deployment, the key in the secret store and the reference in configuration, and the school's Admin sees
/// neither (PC-9, SC-7, §3 v33).
/// <para>
/// The entity guards its own consistency — the address always belongs to the domain the record is bound to —
/// but it does not enforce BR-020, which compares both values against the <c>Installation</c> domain held in
/// <see cref="LegitimacyState"/>. That is another aggregate, so the comparison lives in the use case
/// (US-009 spec FR-006, FR-007).
/// </para>
/// </remarks>
public sealed class WorkspaceConnection
{
    private WorkspaceConnection()
    {
        Domain = string.Empty;
        ImpersonationUserEmail = string.Empty;
    }

    public long Id { get; private set; }

    /// <summary>The Workspace domain this connection is bound to, stored lower-cased.</summary>
    public string Domain { get; private set; }

    /// <summary>The school's technical account, stored lower-cased (BR-015).</summary>
    public string ImpersonationUserEmail { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The first save (US-009 spec FR-006).</summary>
    public static WorkspaceConnection Create(string domain, string impersonationUserEmail)
    {
        var (normalizedDomain, normalizedEmail) = Normalize(domain, impersonationUserEmail);
        return new WorkspaceConnection
        {
            Domain = normalizedDomain,
            ImpersonationUserEmail = normalizedEmail,
        };
    }

    /// <summary>A change of the stored connection; the record keeps its identity (spec FR-006, AC-007).</summary>
    public void ChangeTo(string domain, string impersonationUserEmail)
    {
        var (normalizedDomain, normalizedEmail) = Normalize(domain, impersonationUserEmail);
        Domain = normalizedDomain;
        ImpersonationUserEmail = normalizedEmail;
    }

    /// <summary>The domain part of an address, normalised the way this entity stores it.</summary>
    public static string DomainOf(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var at = email.LastIndexOf('@');
        return at < 0 ? string.Empty : NormalizeDomain(email[(at + 1)..]);
    }

    /// <summary>Trimmed, lower-cased and without one trailing dot (spec VR-002, FR-007).</summary>
    public static string NormalizeDomain(string domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        return domain.Trim().TrimEnd('.').ToLowerInvariant();
    }

    /// <summary>Trimmed and lower-cased, with the domain part normalised (spec VR-001).</summary>
    public static string NormalizeEmail(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        var trimmed = email.Trim().ToLowerInvariant();
        var at = trimmed.LastIndexOf('@');
        return at < 0 ? trimmed : trimmed[..(at + 1)] + NormalizeDomain(trimmed[(at + 1)..]);
    }

    private static (string Domain, string Email) Normalize(string domain, string impersonationUserEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(impersonationUserEmail);

        var normalizedDomain = NormalizeDomain(domain);
        var normalizedEmail = NormalizeEmail(impersonationUserEmail);
        if (!string.Equals(DomainOf(normalizedEmail), normalizedDomain, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The technical account must belong to the domain the connection is bound to.",
                nameof(impersonationUserEmail));
        }

        return (normalizedDomain, normalizedEmail);
    }
}
