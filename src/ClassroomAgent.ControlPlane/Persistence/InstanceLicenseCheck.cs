namespace ClassroomAgent.ControlPlane.Persistence;

/// <summary>
/// The last legitimacy check of one <see cref="Installation"/> (US-005 entity model §2.1): one record per
/// installation, replaced by every call. Not audited and never shown to an installation.
/// </summary>
public class InstanceLicenseCheck
{
    private InstanceLicenseCheck()
    {
    }

    public long Id { get; private set; }

    public long InstallationId { get; private set; }

    public DateTimeOffset AnsweredAt { get; private set; }

    public string ApplicationVersion { get; private set; } = string.Empty;

    public int ContractVersion { get; private set; }

    public InstallationStatus AnsweredStatus { get; private set; }

    public CompatibilityState AnsweredCompatibility { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>A new record; the values are already validated by the request rules (VR-002).</summary>
    public static InstanceLicenseCheck Record(
        long installationId,
        DateTimeOffset answeredAt,
        string applicationVersion,
        int contractVersion,
        InstallationStatus status,
        CompatibilityState compatibility)
    {
        var check = new InstanceLicenseCheck { InstallationId = installationId };
        check.Replace(answeredAt, applicationVersion, contractVersion, status, compatibility);
        return check;
    }

    public void Replace(
        DateTimeOffset answeredAt,
        string applicationVersion,
        int contractVersion,
        InstallationStatus status,
        CompatibilityState compatibility)
    {
        AnsweredAt = answeredAt;
        ApplicationVersion = applicationVersion;
        ContractVersion = contractVersion;
        AnsweredStatus = status;
        AnsweredCompatibility = compatibility;
    }
}
