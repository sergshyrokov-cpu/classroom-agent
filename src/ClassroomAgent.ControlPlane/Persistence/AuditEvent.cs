namespace ClassroomAgent.ControlPlane.Persistence;

/// <summary>
/// An immutable Control Plane audit row (entity model §2.2). Built only through the
/// factories, which accept no string that could carry a login, password or code.
/// </summary>
public class AuditEvent
{
    private AuditEvent()
    {
    }

    public long Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public AuditActorType ActorType { get; private set; }

    public long? ActorId { get; private set; }

    public AuditAction Action { get; private set; }

    public AuditTargetType? TargetType { get; private set; }

    public long? TargetId { get; private set; }

    public AuditOutcome Outcome { get; private set; }

    public AuditRefusalCategory? RefusalCategory { get; private set; }

    public string? RequestId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static AuditEvent OwnerSignInSucceeded(long ownerId, DateTimeOffset occurredAt, string? requestId) =>
        OwnerActs(AuditAction.OwnerSignIn, ownerId, AuditOutcome.Succeeded, null, occurredAt, requestId);

    public static AuditEvent OwnerSignInRefused(
        long ownerId,
        AuditRefusalCategory category,
        DateTimeOffset occurredAt,
        string? requestId)
    {
        if (category is not (AuditRefusalCategory.WrongPassword or AuditRefusalCategory.LockedOut))
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "Only a wrong password or a lockout refuses a known Owner.");
        }

        return OwnerActs(AuditAction.OwnerSignIn, ownerId, AuditOutcome.Refused, category, occurredAt, requestId);
    }

    public static AuditEvent OwnerSignInRefusedUnknownLogin(DateTimeOffset occurredAt, string? requestId) =>
        AnonymousRefused(AuditAction.OwnerSignIn, AuditRefusalCategory.UnknownLogin, occurredAt, requestId);

    public static AuditEvent OwnerFirstRunSetupSucceeded(long ownerId, DateTimeOffset occurredAt, string? requestId) =>
        OwnerActs(AuditAction.OwnerFirstRunSetup, ownerId, AuditOutcome.Succeeded, null, occurredAt, requestId);

    public static AuditEvent OwnerFirstRunSetupRefusedWrongCode(DateTimeOffset occurredAt, string? requestId) =>
        AnonymousRefused(AuditAction.OwnerFirstRunSetup, AuditRefusalCategory.WrongSetupCode, occurredAt, requestId);

    public static AuditEvent InstallationCreated(long ownerId, long installationId, DateTimeOffset occurredAt, string? requestId) =>
        OwnerActsOnInstallation(AuditAction.InstallationCreated, ownerId, installationId, occurredAt, requestId);

    public static AuditEvent InstallationRenamed(long ownerId, long installationId, DateTimeOffset occurredAt, string? requestId) =>
        OwnerActsOnInstallation(AuditAction.InstallationRenamed, ownerId, installationId, occurredAt, requestId);

    public static AuditEvent InstallationClientIdChanged(
        long ownerId,
        long installationId,
        DateTimeOffset occurredAt,
        string? requestId) =>
        OwnerActsOnInstallation(AuditAction.InstallationClientIdChanged, ownerId, installationId, occurredAt, requestId);

    public static AuditEvent InstallationSuspended(long ownerId, long installationId, DateTimeOffset occurredAt, string? requestId) =>
        OwnerActsOnInstallation(AuditAction.InstallationSuspended, ownerId, installationId, occurredAt, requestId);

    public static AuditEvent InstallationResumed(long ownerId, long installationId, DateTimeOffset occurredAt, string? requestId) =>
        OwnerActsOnInstallation(AuditAction.InstallationResumed, ownerId, installationId, occurredAt, requestId);

    public static AuditEvent AllowedAdminAdded(long ownerId, long allowedAdminId, DateTimeOffset occurredAt, string? requestId) =>
        OwnerActsOn(
            AuditAction.AllowedAdminAdded,
            ownerId,
            AuditTargetType.AllowedAdmin,
            allowedAdminId,
            AuditOutcome.Succeeded,
            null,
            occurredAt,
            requestId);

    public static AuditEvent AllowedAdminRevoked(long ownerId, long allowedAdminId, DateTimeOffset occurredAt, string? requestId) =>
        OwnerActsOn(
            AuditAction.AllowedAdminRevoked,
            ownerId,
            AuditTargetType.AllowedAdmin,
            allowedAdminId,
            AuditOutcome.Succeeded,
            null,
            occurredAt,
            requestId);

    private static AuditEvent OwnerActs(
        AuditAction action,
        long ownerId,
        AuditOutcome outcome,
        AuditRefusalCategory? category,
        DateTimeOffset occurredAt,
        string? requestId) =>
        OwnerActsOn(action, ownerId, AuditTargetType.Owner, ownerId, outcome, category, occurredAt, requestId);

    private static AuditEvent OwnerActsOnInstallation(
        AuditAction action,
        long ownerId,
        long installationId,
        DateTimeOffset occurredAt,
        string? requestId) =>
        OwnerActsOn(action, ownerId, AuditTargetType.Installation, installationId, AuditOutcome.Succeeded, null, occurredAt, requestId);

    private static AuditEvent OwnerActsOn(
        AuditAction action,
        long ownerId,
        AuditTargetType targetType,
        long targetId,
        AuditOutcome outcome,
        AuditRefusalCategory? category,
        DateTimeOffset occurredAt,
        string? requestId) =>
        new()
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.Owner,
            ActorId = ownerId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Outcome = outcome,
            RefusalCategory = category,
            RequestId = requestId,
        };

    private static AuditEvent AnonymousRefused(
        AuditAction action,
        AuditRefusalCategory category,
        DateTimeOffset occurredAt,
        string? requestId) =>
        new()
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.Anonymous,
            Action = action,
            Outcome = AuditOutcome.Refused,
            RefusalCategory = category,
            RequestId = requestId,
        };
}
