namespace ClassroomAgent.Domain.Rules;

/// <summary>
/// One connection to a Meet conference as the domain sees it, already validated (US-031 entity model §3, spec FR-008):
/// the endpoint, the email only when it is a domain account's (null = other participant), the join time and the
/// duration in seconds.
/// </summary>
public sealed record MeetConnection(string EndpointId, string? DomainEmail, DateTimeOffset JoinedAt, int DurationSeconds);
