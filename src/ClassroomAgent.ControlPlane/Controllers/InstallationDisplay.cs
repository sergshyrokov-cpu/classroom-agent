using System.Globalization;
using ClassroomAgent.ControlPlane.Persistence;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>How the installation pages show a status and a time (US-002 FR-001, OD-003).</summary>
public static class InstallationDisplay
{
    public static string StatusKey(InstallationStatus status) => status switch
    {
        InstallationStatus.Active => "Installation.Status.Active",
        InstallationStatus.Suspended => "Installation.Status.Suspended",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    /// <summary>The UTC instant as the current culture's short date, <c>HH:mm</c> and <c>UTC</c>; never another zone.</summary>
    public static string UtcTime(DateTimeOffset instant)
    {
        var culture = CultureInfo.CurrentCulture;
        return instant.UtcDateTime.ToString(culture.DateTimeFormat.ShortDatePattern + " HH:mm", culture) + " UTC";
    }
}
