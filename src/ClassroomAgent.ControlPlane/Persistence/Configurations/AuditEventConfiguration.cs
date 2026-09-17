using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.ControlPlane.Persistence.Configurations;

/// <summary>
/// Table <c>audit_event</c> exactly as db-design §4 defines it. Enums are stored as
/// explicit codes, never as integers or member names (entity model §2.2).
/// </summary>
public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_event", table =>
        {
            table.HasCheckConstraint("ck_audit_event_actor_type", "actor_type IN ('owner', 'anonymous')");
            table.HasCheckConstraint("ck_audit_event_actor", "(actor_type = 'anonymous') = (actor_id IS NULL)");
            table.HasCheckConstraint("ck_audit_event_target", "(target_type IS NULL) = (target_id IS NULL)");
            table.HasCheckConstraint("ck_audit_event_outcome", "outcome IN ('succeeded', 'refused')");
            table.HasCheckConstraint(
                "ck_audit_event_refusal_category",
                "(outcome = 'refused') = (refusal_category IS NOT NULL)");
        });

        builder.HasKey(e => e.Id).HasName("pk_audit_event");
        builder.Property(e => e.Id).UseIdentityByDefaultColumn();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.ActorType)
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(v => ActorTypeCode(v), code => ActorTypeFromCode(code));
        builder.Property(e => e.ActorId);
        builder.Property(e => e.Action)
            .HasMaxLength(64)
            .IsRequired()
            .HasConversion(v => ActionCode(v), code => ActionFromCode(code));
        builder.Property(e => e.TargetType)
            .HasMaxLength(32)
            .HasConversion(v => TargetTypeCode(v!.Value), code => TargetTypeFromCode(code));
        builder.Property(e => e.TargetId);
        builder.Property(e => e.Outcome)
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(v => OutcomeCode(v), code => OutcomeFromCode(code));
        builder.Property(e => e.RefusalCategory)
            .HasMaxLength(32)
            .HasConversion(v => RefusalCategoryCode(v!.Value), code => RefusalCategoryFromCode(code));
        builder.Property(e => e.RequestId).HasMaxLength(128);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
    }

    private static string ActorTypeCode(AuditActorType value) => value switch
    {
        AuditActorType.Owner => "owner",
        AuditActorType.Anonymous => "anonymous",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static AuditActorType ActorTypeFromCode(string code) => code switch
    {
        "owner" => AuditActorType.Owner,
        "anonymous" => AuditActorType.Anonymous,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    private static string ActionCode(AuditAction value) => value switch
    {
        AuditAction.OwnerSignIn => "owner_sign_in",
        AuditAction.OwnerFirstRunSetup => "owner_first_run_setup",
        AuditAction.InstallationCreated => "installation_created",
        AuditAction.InstallationRenamed => "installation_renamed",
        AuditAction.InstallationClientIdChanged => "installation_client_id_changed",
        AuditAction.AllowedAdminAdded => "allowed_admin_added",
        AuditAction.AllowedAdminRevoked => "allowed_admin_revoked",
        AuditAction.InstallationSuspended => "installation_suspended",
        AuditAction.InstallationResumed => "installation_resumed",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static AuditAction ActionFromCode(string code) => code switch
    {
        "owner_sign_in" => AuditAction.OwnerSignIn,
        "owner_first_run_setup" => AuditAction.OwnerFirstRunSetup,
        "installation_created" => AuditAction.InstallationCreated,
        "installation_renamed" => AuditAction.InstallationRenamed,
        "installation_client_id_changed" => AuditAction.InstallationClientIdChanged,
        "allowed_admin_added" => AuditAction.AllowedAdminAdded,
        "allowed_admin_revoked" => AuditAction.AllowedAdminRevoked,
        "installation_suspended" => AuditAction.InstallationSuspended,
        "installation_resumed" => AuditAction.InstallationResumed,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    private static string TargetTypeCode(AuditTargetType value) => value switch
    {
        AuditTargetType.Owner => "owner",
        AuditTargetType.Installation => "installation",
        AuditTargetType.AllowedAdmin => "allowed_admin",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static AuditTargetType? TargetTypeFromCode(string code) => code switch
    {
        "owner" => AuditTargetType.Owner,
        "installation" => AuditTargetType.Installation,
        "allowed_admin" => AuditTargetType.AllowedAdmin,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    private static string OutcomeCode(AuditOutcome value) => value switch
    {
        AuditOutcome.Succeeded => "succeeded",
        AuditOutcome.Refused => "refused",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static AuditOutcome OutcomeFromCode(string code) => code switch
    {
        "succeeded" => AuditOutcome.Succeeded,
        "refused" => AuditOutcome.Refused,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    private static string RefusalCategoryCode(AuditRefusalCategory value) => value switch
    {
        AuditRefusalCategory.UnknownLogin => "unknown_login",
        AuditRefusalCategory.WrongPassword => "wrong_password",
        AuditRefusalCategory.LockedOut => "locked_out",
        AuditRefusalCategory.WrongSetupCode => "wrong_setup_code",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static AuditRefusalCategory? RefusalCategoryFromCode(string code) => code switch
    {
        "unknown_login" => AuditRefusalCategory.UnknownLogin,
        "wrong_password" => AuditRefusalCategory.WrongPassword,
        "locked_out" => AuditRefusalCategory.LockedOut,
        "wrong_setup_code" => AuditRefusalCategory.WrongSetupCode,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
