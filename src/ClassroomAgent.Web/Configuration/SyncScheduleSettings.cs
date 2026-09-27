namespace ClassroomAgent.Web.Configuration;

/// <summary>
/// The one setting the synchronization schedule needs (US-013 spec FR-003, FR-013): how long after the
/// completion of a run the next one becomes due.
/// </summary>
/// <remarks>
/// Narrow on purpose, as <c>SchoolDefaults</c> and <c>GoogleServiceAccountSettings</c> are: registering the
/// whole <see cref="InstallationSettings"/> record would make the resolved OAuth client secret and the database
/// connection string injectable anywhere in the host, which SC-7 keeps narrow (US-013 security review F-1).
/// </remarks>
public sealed record SyncScheduleSettings(TimeSpan Interval);
