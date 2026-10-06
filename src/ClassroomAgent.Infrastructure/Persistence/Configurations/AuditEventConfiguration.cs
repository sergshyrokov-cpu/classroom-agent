using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>audit_event</c> exactly as US-008 db-design §4 defines it: the installation's own audit trail, not a
/// copy of the Control Plane's. No column can carry a personal datum, <c>actor_id</c> carries no foreign key
/// because audit rows outlive the accounts they name (PC-9, PC-11), and there is no index at all — the table is
/// write-only until the audit viewer (EPIC-9) and the retention purge (US-037, EPIC-5) bring their own queries (PC-7).
/// US-037 db-design §2 adds the purge row's five counts, its constraints and the one index the purge's delete needs;
/// US-028 db-design §3 adds the six export columns of a journal export and their constraints.
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
            table.HasCheckConstraint(
                "ck_audit_event_action",
                "action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run', "
                + "'dean_account_created', 'dean_account_disabled', 'dean_account_reenabled', "
                + "'dean_account_password_reset', 'dean_password_changed', 'dean_sign_in', 'retention_purge_run', "
                + "'synchronization_requested', 'report_template_created', 'report_template_changed', "
                + "'report_template_deleted', 'journal_exported')");

            // US-009 db-design §4.1: a refused action names WHAT was refused without naming a row that was never
            // created, so a target type without an id is now legal. An id without a type — an identifier belonging
            // to nothing — is still forbidden.
            table.HasCheckConstraint("ck_audit_event_target", "target_id IS NULL OR target_type IS NOT NULL");
            table.HasCheckConstraint(
                "ck_audit_event_target_type_value",
                "target_type IS NULL OR target_type IN ('workspace_connection', 'app_user', 'report_template', 'course')");
            table.HasCheckConstraint("ck_audit_event_outcome", "outcome IN ('succeeded', 'refused')");
            table.HasCheckConstraint(
                "ck_audit_event_refusal_category",
                "(outcome = 'refused') = (refusal_category IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_audit_event_refusal_category_value",
                "refusal_category IS NULL OR refusal_category IN ('not_in_allowed_admin', 'control_plane_unavailable', "
                + "'unknown_installation', 'callback_failed', 'account_disabled', 'domain_mismatch', "
                + "'impersonation_domain_mismatch', 'domain_not_confirmed', 'read_only_mode', 'connection_not_usable', "
                + "'unknown_login', 'wrong_password', 'locked_out')");

            // PC-6 states the equality as a property of the table; turning it into a CHECK costs nothing and
            // catches an update that passes through EF Core, because the interceptor stamps updated_at on a
            // Modified entity. It is not a defence against a hand-written UPDATE that rewrites both (db-design §4.2).
            table.HasCheckConstraint("ck_audit_event_immutable", "updated_at = created_at");

            // US-037 db-design §2.4: the five counts belong to the purge row — all of them there, none anywhere else,
            // never negative — and the purge row is the system's, with no target, no request and no refusal.
            table.HasCheckConstraint(
                "ck_audit_event_purge_counts",
                "(action = 'retention_purge_run') = (purged_courses IS NOT NULL AND purged_leaver_memberships IS NOT NULL "
                + "AND purged_participants IS NOT NULL AND purged_accounts IS NOT NULL AND purged_audit_rows IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_audit_event_purge_counts_absent",
                "action = 'retention_purge_run' OR (purged_courses IS NULL AND purged_leaver_memberships IS NULL "
                + "AND purged_participants IS NULL AND purged_accounts IS NULL AND purged_audit_rows IS NULL)");
            table.HasCheckConstraint(
                "ck_audit_event_purge_counts_non_negative",
                "(purged_courses IS NULL OR purged_courses >= 0) "
                + "AND (purged_leaver_memberships IS NULL OR purged_leaver_memberships >= 0) "
                + "AND (purged_participants IS NULL OR purged_participants >= 0) "
                + "AND (purged_accounts IS NULL OR purged_accounts >= 0) "
                + "AND (purged_audit_rows IS NULL OR purged_audit_rows >= 0)");
            table.HasCheckConstraint(
                "ck_audit_event_purge_actor",
                "action <> 'retention_purge_run' OR (actor_type = 'system' AND target_type IS NULL "
                + "AND target_id IS NULL AND outcome = 'succeeded' AND request_id IS NULL)");

            // US-019 db-design §2.3: a manual synchronization request is always a user's, never targets a row, and is
            // refused only for the two reasons its use case has.
            table.HasCheckConstraint(
                "ck_audit_event_sync_request_shape",
                "action <> 'synchronization_requested' OR (actor_type = 'app_user' AND target_type IS NULL "
                + "AND target_id IS NULL AND (refusal_category IS NULL "
                + "OR refusal_category IN ('read_only_mode', 'connection_not_usable')))");

            // US-027 db-design §4: a template action is a user's, targets a template, is refused only by read-only
            // mode, and a succeeded row names the template.
            table.HasCheckConstraint(
                "ck_audit_event_report_template_shape",
                "action NOT IN ('report_template_created', 'report_template_changed', 'report_template_deleted') "
                + "OR (actor_type = 'app_user' AND target_type = 'report_template' "
                + "AND (refusal_category IS NULL OR refusal_category = 'read_only_mode') "
                + "AND (outcome <> 'succeeded' OR target_id IS NOT NULL) "
                + "AND (action <> 'report_template_created' OR outcome = 'succeeded' OR target_id IS NULL))");

            // US-028 db-design §3.4: the export details belong to the export row — all required ones there, none
            // anywhere else; an export is a user's, names the course, is audited only when it succeeded, and records
            // exactly one form of the template.
            table.HasCheckConstraint(
                "ck_audit_event_export_columns",
                "(action = 'journal_exported') = (export_period_from IS NOT NULL AND export_period_to IS NOT NULL "
                + "AND export_template_built_in IS NOT NULL AND export_rows IS NOT NULL AND export_format IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_audit_event_export_columns_absent",
                "action = 'journal_exported' OR (export_period_from IS NULL AND export_period_to IS NULL "
                + "AND export_template_id IS NULL AND export_template_built_in IS NULL AND export_rows IS NULL "
                + "AND export_format IS NULL)");
            table.HasCheckConstraint(
                "ck_audit_event_export_shape",
                "action <> 'journal_exported' OR (actor_type = 'app_user' AND target_type = 'course' "
                + "AND target_id IS NOT NULL AND outcome = 'succeeded' AND export_period_from <= export_period_to "
                + "AND export_rows >= 0 AND export_format IN ('xlsx') "
                + "AND export_template_built_in = (export_template_id IS NULL))");
            table.HasCheckConstraint(
                "ck_audit_event_export_template_id",
                "export_template_id IS NULL OR export_template_id > 0");
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
        builder.Property(e => e.PurgedCourses);
        builder.Property(e => e.PurgedLeaverMemberships);
        builder.Property(e => e.PurgedParticipants);
        builder.Property(e => e.PurgedAccounts);
        builder.Property(e => e.PurgedAuditRows);

        // US-028 db-design §3.2: calendar days of the school, a bare template id (no foreign key, PC-9), a count and
        // a closed format code.
        builder.Property(e => e.ExportPeriodFrom).HasColumnType("date");
        builder.Property(e => e.ExportPeriodTo).HasColumnType("date");
        builder.Property(e => e.ExportTemplateId);
        builder.Property(e => e.ExportTemplateBuiltIn);
        builder.Property(e => e.ExportRows);
        builder.Property(e => e.ExportFormat)
            .HasMaxLength(8)
            .HasConversion(v => NullableExportFormatCode(v), code => NullableExportFormatFromCode(code));

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        // US-037 db-design §2.5: the purge deletes by occurred_at once a day over a table that grows for N years.
        builder.HasIndex(e => e.OccurredAt).HasDatabaseName("ix_audit_event_occurred_at");
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
        AuditAction.WorkspaceConnectionSaved => "workspace_connection_saved",
        AuditAction.AccessCheckRun => "access_check_run",
        AuditAction.DeanAccountCreated => "dean_account_created",
        AuditAction.DeanAccountDisabled => "dean_account_disabled",
        AuditAction.DeanAccountReEnabled => "dean_account_reenabled",
        AuditAction.DeanAccountPasswordReset => "dean_account_password_reset",
        AuditAction.DeanPasswordChanged => "dean_password_changed",
        AuditAction.DeanSignIn => "dean_sign_in",
        AuditAction.RetentionPurgeRun => "retention_purge_run",
        AuditAction.SynchronizationRequested => "synchronization_requested",
        AuditAction.ReportTemplateCreated => "report_template_created",
        AuditAction.ReportTemplateChanged => "report_template_changed",
        AuditAction.ReportTemplateDeleted => "report_template_deleted",
        AuditAction.JournalExported => "journal_exported",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static AuditAction ActionFromCode(string code) => code switch
    {
        "admin_sign_in" => AuditAction.AdminSignIn,
        "workspace_connection_saved" => AuditAction.WorkspaceConnectionSaved,
        "access_check_run" => AuditAction.AccessCheckRun,
        "dean_account_created" => AuditAction.DeanAccountCreated,
        "dean_account_disabled" => AuditAction.DeanAccountDisabled,
        "dean_account_reenabled" => AuditAction.DeanAccountReEnabled,
        "dean_account_password_reset" => AuditAction.DeanAccountPasswordReset,
        "dean_password_changed" => AuditAction.DeanPasswordChanged,
        "dean_sign_in" => AuditAction.DeanSignIn,
        "retention_purge_run" => AuditAction.RetentionPurgeRun,
        "synchronization_requested" => AuditAction.SynchronizationRequested,
        "report_template_created" => AuditAction.ReportTemplateCreated,
        "report_template_changed" => AuditAction.ReportTemplateChanged,
        "report_template_deleted" => AuditAction.ReportTemplateDeleted,
        "journal_exported" => AuditAction.JournalExported,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    /// <summary>US-009 gives <see cref="AuditTargetType"/> its first member (US-008 spec I-11).</summary>
    private static string TargetTypeCode(AuditTargetType value) => value switch
    {
        AuditTargetType.WorkspaceConnection => "workspace_connection",
        AuditTargetType.AppUser => "app_user",
        AuditTargetType.ReportTemplate => "report_template",
        AuditTargetType.Course => "course",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static AuditTargetType TargetTypeFromCode(string code) => code switch
    {
        "workspace_connection" => AuditTargetType.WorkspaceConnection,
        "app_user" => AuditTargetType.AppUser,
        "report_template" => AuditTargetType.ReportTemplate,
        "course" => AuditTargetType.Course,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    private static string? NullableExportFormatCode(ExportFormat? value) => value switch
    {
        null => null,
        ExportFormat.Xlsx => "xlsx",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static ExportFormat? NullableExportFormatFromCode(string? code) => code switch
    {
        null => null,
        "xlsx" => ExportFormat.Xlsx,
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
        AuditRefusalCategory.NotInAllowedAdmin => "not_in_allowed_admin",
        AuditRefusalCategory.ControlPlaneUnavailable => "control_plane_unavailable",
        AuditRefusalCategory.UnknownInstallation => "unknown_installation",
        AuditRefusalCategory.CallbackFailed => "callback_failed",
        AuditRefusalCategory.AccountDisabled => "account_disabled",
        AuditRefusalCategory.DomainMismatch => "domain_mismatch",
        AuditRefusalCategory.ImpersonationDomainMismatch => "impersonation_domain_mismatch",
        AuditRefusalCategory.DomainNotConfirmed => "domain_not_confirmed",
        AuditRefusalCategory.ReadOnlyMode => "read_only_mode",
        AuditRefusalCategory.ConnectionNotUsable => "connection_not_usable",
        AuditRefusalCategory.UnknownLogin => "unknown_login",
        AuditRefusalCategory.WrongPassword => "wrong_password",
        AuditRefusalCategory.LockedOut => "locked_out",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static AuditRefusalCategory RefusalCategoryFromCode(string code) => code switch
    {
        "not_in_allowed_admin" => AuditRefusalCategory.NotInAllowedAdmin,
        "control_plane_unavailable" => AuditRefusalCategory.ControlPlaneUnavailable,
        "unknown_installation" => AuditRefusalCategory.UnknownInstallation,
        "callback_failed" => AuditRefusalCategory.CallbackFailed,
        "account_disabled" => AuditRefusalCategory.AccountDisabled,
        "domain_mismatch" => AuditRefusalCategory.DomainMismatch,
        "impersonation_domain_mismatch" => AuditRefusalCategory.ImpersonationDomainMismatch,
        "domain_not_confirmed" => AuditRefusalCategory.DomainNotConfirmed,
        "read_only_mode" => AuditRefusalCategory.ReadOnlyMode,
        "connection_not_usable" => AuditRefusalCategory.ConnectionNotUsable,
        "unknown_login" => AuditRefusalCategory.UnknownLogin,
        "wrong_password" => AuditRefusalCategory.WrongPassword,
        "locked_out" => AuditRefusalCategory.LockedOut,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
