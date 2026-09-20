namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// A stored installation <c>audit_event</c> row (US-008 db-design 4). It is deliberately a separate type
/// from <see cref="AuditRow"/>: the installation's table is not a copy of the Control Plane's — it carries
/// <c>actor_role</c>, and its actions and refusal categories differ.
/// </summary>
public sealed record InstallationAuditRow(
    long Id,
    DateTimeOffset OccurredAt,
    string ActorType,
    long? ActorId,
    string? ActorRole,
    string Action,
    string? TargetType,
    long? TargetId,
    string Outcome,
    string? RefusalCategory,
    string? RequestId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
