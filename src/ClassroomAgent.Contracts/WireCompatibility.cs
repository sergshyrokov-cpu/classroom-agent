namespace ClassroomAgent.Contracts;

/// <summary>The <c>compatibility</c> values of <see cref="LegitimacyCheckResponse"/> (US-005 api-design §4, DC-12).</summary>
public static class WireCompatibility
{
    public const string Supported = "supported";

    public const string UpgradeRecommended = "upgrade_recommended";

    public const string UpgradeRequired = "upgrade_required";
}
