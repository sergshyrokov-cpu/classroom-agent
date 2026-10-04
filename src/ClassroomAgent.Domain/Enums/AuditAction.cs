namespace ClassroomAgent.Domain.Enums;

/// <summary>
/// The audited actions of the installation (US-008 spec FR-012; <c>trebovaniya.md</c> §5). The list grows in
/// the Story that performs the action, never ahead of it (spec I-11).
/// </summary>
public enum AuditAction
{
    AdminSignIn,

    /// <summary>
    /// US-009 spec FR-009: saving or changing the <c>WorkspaceConnection</c>. One action for both, as
    /// <c>trebovaniya.md</c> §5 names them in one breath; which it was follows from the row it targets (spec I-2).
    /// </summary>
    WorkspaceConnectionSaved,

    /// <summary>
    /// US-011 spec FR-008: running "Проверить доступ" (<c>trebovaniya.md</c> §5). The row records that the check was
    /// carried out or refused — never what it found (spec I-4, OD-003).
    /// </summary>
    AccessCheckRun,

    /// <summary>US-012 spec FR-017: an Admin created a Dean account (SC-11, BR-014).</summary>
    DeanAccountCreated,

    /// <summary>US-012 spec FR-017: an Admin disabled a Dean account.</summary>
    DeanAccountDisabled,

    /// <summary>US-012 spec FR-017: an Admin re-enabled a disabled Dean account.</summary>
    DeanAccountReEnabled,

    /// <summary>US-012 spec FR-017: an Admin reset a Dean account's password to a new temporary one.</summary>
    DeanAccountPasswordReset,

    /// <summary>
    /// US-012 spec FR-017: a Dean changed their own password — at the forced change or later. One action for
    /// both, as <c>trebovaniya.md</c> §5 names it once (db-design §4.1).
    /// </summary>
    DeanPasswordChanged,

    /// <summary>
    /// US-012 spec FR-017: a Dean's sign-in, succeeded or refused. It mirrors <see cref="AdminSignIn"/>; the
    /// outcome tells the two apart and the refusal category says why (db-design §4.1).
    /// </summary>
    DeanSignIn,

    /// <summary>
    /// US-037 spec FR-010: one retention purge run, actor <c>system</c>, carrying only the five counts of
    /// <see cref="Rules.RetentionPurgeCounts"/> (§5 v47, SC-11).
    /// </summary>
    RetentionPurgeRun,

    /// <summary>
    /// US-019 spec FR-005: a manual start of synchronization (<c>trebovaniya.md</c> §5 "ручной запуск
    /// синхронизации"), accepted or refused. <c>succeeded</c> means the request was accepted, never that a run
    /// happened (spec I-2). Performed by an Admin or a Dean, so the role is recorded from the session.
    /// </summary>
    SynchronizationRequested,

    /// <summary>US-027 spec FR-016: a report template was created — from scratch or as a copy.</summary>
    ReportTemplateCreated,

    /// <summary>US-027 spec FR-016: a report template's settings were saved.</summary>
    ReportTemplateChanged,

    /// <summary>US-027 spec FR-016: a report template was deleted.</summary>
    ReportTemplateDeleted,
}
