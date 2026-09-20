using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>audit_event</c> exactly as US-008 db-design §4 defines it: the installation's own audit trail, not a
/// copy of the Control Plane's. No column can carry a personal datum, <c>actor_id</c> carries no foreign key
/// because audit rows outlive the accounts they name (PC-9, PC-11), and there is no index at all — the table is
/// write-only until the audit viewer (EPIC-9) and the retention purge (EPIC-10) bring their own queries (PC-7).
/// </summary>
public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_event", table =>
        {
            table.HasCheckConstraint("ck_audit_event_actor_type", "actor_type IN ('app_user', 'anonymous', 'system')");
            table.HasCheckConstraint("ck_audit_event_actor_id", "(actor_type = 'app_user') = (actor_id IS NOT NULL)");
            table.HasCheckConstraint("ck_audit_event_actor_role", "(actor_type = 'app_user') = (actor_role IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_audit_event_actor_role_value",
                "actor_role IS NULL OR actor_role IN ('admin', 'dean')");
            table.HasCheckConstraint("ck_audit_event_action", "action IN ('admin_sign_in')");
            table.HasCheckConstraint("ck_audit_event_target", "(target_type IS NULL) = (target_id IS NULL)");
            table.HasCheckConstraint("ck_audit_event_outcome", "outcome IN ('succeeded', 'refused')");
            table.HasCheckConstraint(
                "ck_audit_event_refusal_category",
                "(outcome = 'refused') = (refusal_category IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_audit_event_refusal_category_value",
                "refusal_category IS NULL OR refusal_category IN ('not_in_allowed_admin', 'control_plane_unavailable', "
                + "'unknown_installation', 'callback_failed', 'account_disabled')");

            // PC-6 states the equality as a property of the table; turning it into a CHECK costs nothing and
            // catches an update that passes through EF Core, because the interceptor stamps updated_at on a
            // Modified entity. It is not a defence against a hand-written UPDATE that rewrites both (db-design §4.2).
            table.HasCheckConstraint("ck_audit_event_immutable", "updated_at = created_at");
        });

        builder.HasKey(e => e.Id).HasName("pk_audit_event");
        builder.Property(e => e.Id).UseIdentityByDefaultColumn();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.ActorType)
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(v => ActorTypeCode(v), code => ActorTypeFromCode(code));

        // A bare identifier: no navigation property and no foreign key (PC-9).
        builder.Property(e => e.ActorId);
        builder.Property(e => e.ActorRole)
            .HasMaxLength(16)
            .HasConversion(v => NullableRoleCode(v), code => NullableRoleFromCode(code));
        builder.Property(e => e.Action)
            .HasMaxLength(64)
            .IsRequired()
            .HasConversion(v => ActionCode(v), code => ActionFromCode(code));
        builder.Property(e => e.TargetType)
            .HasMaxLength(32)
            .HasConversion(v => NullableTargetTypeCode(v), code => NullableTargetTypeFromCode(code));
        builder.Property(e => e.TargetId);
        builder.Property(e => e.Outcome)
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(v => OutcomeCode(v), code => OutcomeFromCode(code));
        builder.Property(e => e.RefusalCategory)
            .HasMaxLength(32)
            .HasConversion(v => NullableRefusalCategoryCode(v), code => NullableRefusalCategoryFromCode(code));
        builder.Property(e => e.RequestId).HasMaxLength(128);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
    }

    private static string ActorTypeCode(AuditActorType value) => value switch
    {
        AuditActorType.AppUser => "app_user",
        AuditActorType.Anonymous => "anonymous",
        AuditActorType.System => "system",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static AuditActorType ActorTypeFromCode(string code) => code switch
    {
        "app_user" => AuditActorType.AppUser,
        "anonymous" => AuditActorType.Anonymous,
        "system" => AuditActorType.System,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    private static string? NullableRoleCode(AppRole? value) => value is null ? null : RoleCode(value.Value);

    private static AppRole? NullableRoleFromCode(string? code) => code is null ? null : RoleFromCode(code);

    private static string? NullableTargetTypeCode(AuditTargetType? value) =>
        value is null ? null : TargetTypeCode(value.Value);

    private static AuditTargetType? NullableTargetTypeFromCode(string? code) =>
        code is null ? null : TargetTypeFromCode(code);

    private static string? NullableRefusalCategoryCode(AuditRefusalCategory? value) =>
        value is null ? null : RefusalCategoryCode(value.Value);

    private static AuditRefusalCategory? NullableRefusalCategoryFromCode(string? code) =>
        code is null ? null : RefusalCategoryFromCode(code);

    private static string RoleCode(AppRole value) => value switch
    {
        AppRole.Admin => "admin",
        AppRole.Dean => "dean",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static AppRole RoleFromCode(string code) => code switch
    {
        "admin" => AppRole.Admin,
        "dean" => AppRole.Dean,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    private static string ActionCode(AuditAction value) => value switch
    {
        AuditAction.AdminSignIn => "admin_sign_in",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static AuditAction ActionFromCode(string code) => code switch
    {
        "admin_sign_in" => AuditAction.AdminSignIn,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    /// <summary><see cref="AuditTargetType"/> has no member in this Story, so no value is ever converted.</summary>
    private static string TargetTypeCode(AuditTargetType value) =>
        throw new ArgumentOutOfRangeException(nameof(value), value, "No target type exists in US-008.");

    private static AuditTargetType TargetTypeFromCode(string code) =>
        throw new ArgumentOutOfRangeException(nameof(code), code, "No target type exists in US-008.");

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
        AuditRefusalCategory.NotInAllowedAdmin => "not_in_allowed_admin",
        AuditRefusalCategory.ControlPlaneUnavailable => "control_plane_unavailable",
        AuditRefusalCategory.UnknownInstallation => "unknown_installation",
        AuditRefusalCategory.CallbackFailed => "callback_failed",
        AuditRefusalCategory.AccountDisabled => "account_disabled",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static AuditRefusalCategory RefusalCategoryFromCode(string code) => code switch
    {
        "not_in_allowed_admin" => AuditRefusalCategory.NotInAllowedAdmin,
        "control_plane_unavailable" => AuditRefusalCategory.ControlPlaneUnavailable,
        "unknown_installation" => AuditRefusalCategory.UnknownInstallation,
        "callback_failed" => AuditRefusalCategory.CallbackFailed,
        "account_disabled" => AuditRefusalCategory.AccountDisabled,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
