using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Web.Configuration;

/// <summary>
/// The installation's validated settings of US-005 spec FR-001 (api-design §10), the private-port address of
/// US-006 spec FR-001, and the four US-008 spec FR-001 makes required — the Data Protection key directory, the
/// public base address, the OAuth client id and the resolved OAuth client secret — plus the optional school
/// default language. None of them is stored in the database.
/// </summary>
/// <remarks>
/// <see cref="OAuthClientSecret"/> is the <em>resolved</em> secret, read once at start-up from the store the
/// reference names (OD-004, option 1). The reference itself is configuration; the secret never is, and neither is
/// ever logged (SC-7, SC-10).
/// </remarks>
/// <param name="RetentionYears">
/// US-015 spec FR-012, VR-008: the retention period N in whole years. Required with no default — an
/// installation without it refuses to start (DC-3, PC-11, <c>trebovaniya.md</c> §5).
/// </param>
/// <param name="TimeZone">
/// US-025 spec FR-010: the school's time zone, resolved from its IANA id. Required — an installation without a
/// valid one refuses to start (NFR-074, DC-3).
/// </param>
/// <param name="SyncInterval">
/// US-013 spec FR-013: the optional synchronization run interval; the default is applied by the code that reads
/// it, not here.
/// </param>
/// <param name="MeetLinking">
/// US-032 spec FR-005: the optional automatic-linking thresholds; null means <see cref="MeetLinkingThresholds.Default"/>.
/// </param>
public sealed record InstallationSettings(
    Guid InstallationId,
    Uri ControlPlaneAddress,
    int PrivatePort,
    string PrivateAddress,
    string ConnectionString,
    string DataProtectionKeyDirectory,
    Uri PublicBaseAddress,
    string OAuthClientId,
    string OAuthClientSecret,
    UiLanguage DefaultUiLanguage,
    int RetentionYears,
    TimeZoneInfo TimeZone,
    string? ServiceAccountKeyReference = null,
    TimeSpan SyncInterval = default,
    MeetLinkingThresholds? MeetLinking = null);
