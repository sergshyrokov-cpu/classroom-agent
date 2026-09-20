using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// One row of the installation's audit trail (US-008 entity model §2.2; <c>trebovaniya.md</c> §5, SC-11). It is
/// not a copy of the Control Plane's table: the actor types, actions and refusal categories differ, and the two
/// databases never share a schema.
/// </summary>
/// <remarks>
/// Immutable: private setters, no mutator, and factories that accept no free string which could carry an email,
/// a token or an authorization code. A row is never updated and is deleted only by the retention purge
/// (PC-9, PC-11). The only text a caller supplies is the request id, which the host generates.
/// </remarks>
public sealed class AuditEvent
{
    private AuditEvent()
    {
    }

    public long Id { get; private set; }

    /// <summary>UTC, from the injectable clock.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    public AuditActorType ActorType { get; private set; }

    /// <summary>The <see cref="AppUser"/> id. A bare identifier: no navigation property and no foreign key (PC-9).</summary>
    public long? ActorId { get; private set; }

    /// <summary>The role at the time of the action.</summary>
    public AppRole? ActorRole { get; private set; }

    public AuditAction Action { get; private set; }

    /// <summary>Unused by this Story; the column set is fixed by SC-11.</summary>
    public AuditTargetType? TargetType { get; private set; }

    /// <summary>Unused by this Story.</summary>
    public long? TargetId { get; private set; }

    public AuditOutcome Outcome { get; private set; }

    /// <summary>Null on success; otherwise why the sign-in was refused.</summary>
    public AuditRefusalCategory? RefusalCategory { get; private set; }

    /// <summary>Ties the row to its log line (SC-11).</summary>
    public string? RequestId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>A successful Admin sign-in, attributed to the account it created or reused (db-design §4.4).</summary>
    public static AuditEvent AdminSignInSucceeded(long appUserId, DateTimeOffset occurredAt, string? requestId) =>
        new()
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.AppUser,
            ActorId = appUserId,
            ActorRole = AppRole.Admin,
            Action = AuditAction.AdminSignIn,
            Outcome = AuditOutcome.Succeeded,
            RefusalCategory = null,
            RequestId = requestId,
        };

    /// <summary>
    /// A refused sign-in of someone who already has an account here — a revoked Admin included (BR-012). The
    /// category must be one that can apply to a known account: a failed callback never reaches an identity, so
    /// it is always anonymous (db-design §4.4).
    /// </summary>
    public static AuditEvent AdminSignInRefused(
        long appUserId,
        AppRole role,
        AuditRefusalCategory category,
        DateTimeOffset occurredAt,
        string? requestId)
    {
        if (category == AuditRefusalCategory.CallbackFailed)
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "A failed callback is always attributed anonymously: it never reached a trustworthy identity.");
        }

        return new AuditEvent
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.AppUser,
            ActorId = appUserId,
            ActorRole = role,
            Action = AuditAction.AdminSignIn,
            Outcome = AuditOutcome.Refused,
            RefusalCategory = category,
            RequestId = requestId,
        };
    }

    /// <summary>A refused sign-in with no account to attribute it to: anonymous, without any identifier (§5 v45).</summary>
    public static AuditEvent AdminSignInRefusedAnonymous(
        AuditRefusalCategory category,
        DateTimeOffset occurredAt,
        string? requestId) =>
        new()
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.Anonymous,
            ActorId = null,
            ActorRole = null,
            Action = AuditAction.AdminSignIn,
            Outcome = AuditOutcome.Refused,
            RefusalCategory = category,
            RequestId = requestId,
        };
}
