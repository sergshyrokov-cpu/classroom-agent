namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>A stored <c>audit_event</c> row, read with raw SQL (db-design §4).</summary>
public sealed record AuditRow(
    long Id,
    DateTimeOffset OccurredAt,
    string ActorType,
    long? ActorId,
    string Action,
    string? TargetType,
    long? TargetId,
    string Outcome,
    string? RefusalCategory,
    string? RequestId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
