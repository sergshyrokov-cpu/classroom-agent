using ClassroomAgent.ControlPlane.Persistence;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// The compatibility decision of US-005 spec FR-005: an unsupported contract version or an application
/// version below the configured minimum is <c>upgrade_required</c>; below the configured recommended
/// version, <c>upgrade_recommended</c>; otherwise <c>supported</c>. Unset settings impose nothing (I-8).
/// </summary>
public sealed class CompatibilityPolicy(InstallationVersion? minimumSupported, InstallationVersion? recommended)
{
    public const string MinimumSupportedVersionKey = "Compatibility:MinimumSupportedVersion";

    public const string RecommendedVersionKey = "Compatibility:RecommendedVersion";

    /// <summary>The contract versions this Control Plane serves (spec I-7).</summary>
    public static IReadOnlySet<int> SupportedContractVersions { get; } = new HashSet<int> { 1 };

    /// <summary>
    /// Reads the optional version settings; a setting that is set but not a valid version stops the host,
    /// naming the setting and never its value (VR-004).
    /// </summary>
    public static CompatibilityPolicy FromConfiguration(IConfiguration configuration) =>
        new(OptionalVersion(configuration, MinimumSupportedVersionKey), OptionalVersion(configuration, RecommendedVersionKey));

    /// <param name="applicationVersion">Already validated by the request rules (VR-002).</param>
    /// <param name="contractVersion">Already validated by the request rules (VR-002).</param>
    public CompatibilityState Decide(InstallationVersion applicationVersion, int contractVersion)
    {
        if (!SupportedContractVersions.Contains(contractVersion)
            || (minimumSupported is { } minimum && applicationVersion < minimum))
        {
            return CompatibilityState.UpgradeRequired;
        }

        return recommended is { } recommendedVersion && applicationVersion < recommendedVersion
            ? CompatibilityState.UpgradeRecommended
            : CompatibilityState.Supported;
    }

    private static InstallationVersion? OptionalVersion(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return InstallationVersion.TryParse(value, out var version)
            ? version
            : throw new InvalidOperationException(
                $"The Control Plane setting '{key}' is not a valid version: expected MAJOR.MINOR.PATCH.");
    }
}
