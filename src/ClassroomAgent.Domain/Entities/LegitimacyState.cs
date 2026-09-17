using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// The installation's record of its legitimacy (US-005 entity model §3.1; <c>trebovaniya.md</c> §3): the last
/// successful check, and the status, compatibility state, domain and client ID of the last recorded answer.
/// One per installation database.
/// </summary>
public sealed class LegitimacyState
{
    private LegitimacyState()
    {
        Domain = string.Empty;
        ClientId = string.Empty;
    }

    public long Id { get; private set; }

    /// <summary>Null until a check succeeds.</summary>
    public DateTimeOffset? LastSuccessfulCheckAt { get; private set; }

    public InstallationStatus Status { get; private set; }

    public CompatibilityState Compatibility { get; private set; }

    public string Domain { get; private set; }

    public string ClientId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static LegitimacyState FromSuccess(
        DateTimeOffset checkedAt,
        InstallationStatus status,
        CompatibilityState compatibility,
        string domain,
        string clientId)
    {
        var state = new LegitimacyState();
        state.RecordSuccess(checkedAt, status, compatibility, domain, clientId);
        return state;
    }

    public static LegitimacyState FromUpgradeRequired(InstallationStatus status, string domain, string clientId)
    {
        var state = new LegitimacyState();
        state.RecordUpgradeRequired(status, domain, clientId);
        return state;
    }

    /// <summary>A successful check: every field from the answer. An <c>upgrade_required</c> answer is never a success.</summary>
    public void RecordSuccess(
        DateTimeOffset checkedAt,
        InstallationStatus status,
        CompatibilityState compatibility,
        string domain,
        string clientId)
    {
        if (compatibility == CompatibilityState.UpgradeRequired)
        {
            throw new ArgumentException("An upgrade_required answer is not a successful check.", nameof(compatibility));
        }

        LastSuccessfulCheckAt = checkedAt;
        Status = status;
        Compatibility = compatibility;
        Domain = domain;
        ClientId = clientId;
    }

    /// <summary>An <c>upgrade_required</c> answer: the answer is kept, the last successful check is not moved.</summary>
    public void RecordUpgradeRequired(InstallationStatus status, string domain, string clientId)
    {
        Status = status;
        Compatibility = CompatibilityState.UpgradeRequired;
        Domain = domain;
        ClientId = clientId;
    }
}
