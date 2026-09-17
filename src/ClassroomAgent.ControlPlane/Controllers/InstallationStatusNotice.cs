using ClassroomAgent.ControlPlane.Persistence;
using Microsoft.Extensions.Primitives;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// The one-time "unchanged" notice of the detail page (US-004 api-design §4): a closed set of query
/// values, rendered only when the value matches the installation's current status and never echoed.
/// </summary>
public static class InstallationStatusNotice
{
    public const string AlreadySuspended = "already-suspended";

    public const string AlreadyActive = "already-active";

    public static string DetailPathWithNotice(Guid identifier, InstallationStatus status) =>
        $"/installations/{identifier:D}?notice={ValueFor(status)}";

    /// <summary>The translation key to show, or null when the query value is absent, repeated, unknown or stale.</summary>
    public static string? KeyFor(StringValues notice, InstallationStatus status) =>
        notice.Count == 1 && string.Equals(notice[0], ValueFor(status), StringComparison.Ordinal)
            ? status == InstallationStatus.Suspended ? "Installation.Status.AlreadySuspended" : "Installation.Status.AlreadyActive"
            : null;

    private static string ValueFor(InstallationStatus status) =>
        status == InstallationStatus.Suspended ? AlreadySuspended : AlreadyActive;
}
