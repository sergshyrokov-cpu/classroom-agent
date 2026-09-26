namespace ClassroomAgent.Infrastructure.Google;

/// <summary>
/// The installation setting that names the service-account key in the secret store (US-011 spec FR-016). Not a
/// start-up requirement (spec I-1): a missing reference is the outcome <c>KeyUnavailable</c>.
/// </summary>
public sealed record GoogleServiceAccountSettings(string? KeyReference);
