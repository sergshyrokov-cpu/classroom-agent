using ClassroomAgent.ControlPlane.Persistence;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>The outcome of a legitimacy check call (US-005 api-design §11; AD-9 — a result, not an exception).</summary>
public abstract record LegitimacyCheckResult
{
    private LegitimacyCheckResult()
    {
    }

    public static LegitimacyCheckResult UnknownInstallation { get; } = new Unknown();

    /// <summary>A known installation: the stored status, the computed compatibility, its domain and client ID.</summary>
    public sealed record Known(InstallationStatus Status, CompatibilityState Compatibility, string Domain, string ClientId)
        : LegitimacyCheckResult;

    private sealed record Unknown : LegitimacyCheckResult;
}
