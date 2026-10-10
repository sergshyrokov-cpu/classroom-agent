using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;

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

    /// <summary>US-037 db-design §2.2: expired courses deleted by a purge run; null for every other action.</summary>
    public int? PurgedCourses { get; private set; }

    /// <summary>US-037 db-design §2.2: leavers' memberships deleted (spec FR-005 only); null for every other action.</summary>
    public int? PurgedLeaverMemberships { get; private set; }

    /// <summary>US-037 db-design §2.2: orphaned participants deleted; null for every other action.</summary>
    public int? PurgedParticipants { get; private set; }

    /// <summary>US-037 db-design §2.2: <c>app_user</c> rows deleted; null for every other action.</summary>
    public int? PurgedAccounts { get; private set; }

    /// <summary>US-037 db-design §2.2: <c>audit_event</c> rows deleted; null for every other action.</summary>
    public int? PurgedAuditRows { get; private set; }

    /// <summary>
    /// US-031 db-design §5: Meet meetings deleted; null for every other action and on purge rows written before US-031.
    /// </summary>
    public int? PurgedMeetSessions { get; private set; }

    /// <summary>US-031 db-design §5: Meet connections deleted; null exactly when <see cref="PurgedMeetSessions"/> is.</summary>
    public int? PurgedMeetParticipations { get; private set; }

    /// <summary>US-028 db-design §3.2: first day of the exported period; null for every other action.</summary>
    public DateOnly? ExportPeriodFrom { get; private set; }

    /// <summary>US-028 db-design §3.2: last day of the exported period; null for every other action.</summary>
    public DateOnly? ExportPeriodTo { get; private set; }

    /// <summary>US-028 db-design §3.2: id of the template used; null for every other action.</summary>
    public long? ExportTemplateId { get; private set; }

    /// <summary>US-028 db-design §3.2: whether the template was the built-in one; null for every other action.</summary>
    public bool? ExportTemplateBuiltIn { get; private set; }

    /// <summary>US-028 db-design §3.2: number of student rows exported; null for every other action.</summary>
    public int? ExportRows { get; private set; }

    /// <summary>US-028 db-design §3.2: file format of the export; null for every other action.</summary>
    public ExportFormat? ExportFormat { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// US-037 spec FR-010: the one row a retention purge run writes — actor <c>system</c>, no target, succeeded, no
    /// request id, and the five counts. Rejects a negative count (VR-003).
    /// </summary>
    public static AuditEvent RetentionPurgeRun(RetentionPurgeCounts counts, DateTimeOffset occurredAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(counts.Courses, nameof(counts));
        ArgumentOutOfRangeException.ThrowIfNegative(counts.LeaverMemberships, nameof(counts));
        ArgumentOutOfRangeException.ThrowIfNegative(counts.Participants, nameof(counts));
        ArgumentOutOfRangeException.ThrowIfNegative(counts.Accounts, nameof(counts));
        ArgumentOutOfRangeException.ThrowIfNegative(counts.AuditRows, nameof(counts));
        ArgumentOutOfRangeException.ThrowIfNegative(counts.MeetSessions, nameof(counts));
        ArgumentOutOfRangeException.ThrowIfNegative(counts.MeetParticipations, nameof(counts));

        return new AuditEvent
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.System,
            ActorId = null,
            ActorRole = null,
            Action = AuditAction.RetentionPurgeRun,
            TargetType = null,
            TargetId = null,
            Outcome = AuditOutcome.Succeeded,
            RefusalCategory = null,
            RequestId = null,
            PurgedCourses = counts.Courses,
            PurgedLeaverMemberships = counts.LeaverMemberships,
            PurgedParticipants = counts.Participants,
            PurgedAccounts = counts.Accounts,
            PurgedAuditRows = counts.AuditRows,
            PurgedMeetSessions = counts.MeetSessions,
            PurgedMeetParticipations = counts.MeetParticipations,
        };
    }

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

    /// <summary>
    /// A saved or changed <c>WorkspaceConnection</c> (US-009 spec FR-009; db-design §4.2). One action serves
    /// both, naming the connection row the save produced.
    /// </summary>
    public static AuditEvent WorkspaceConnectionSaved(
        long appUserId,
        long connectionId,
        DateTimeOffset occurredAt,
        string? requestId) =>
        new()
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.AppUser,
            ActorId = appUserId,
            ActorRole = AppRole.Admin,
            Action = AuditAction.WorkspaceConnectionSaved,
            TargetType = AuditTargetType.WorkspaceConnection,
            TargetId = connectionId,
            Outcome = AuditOutcome.Succeeded,
            RefusalCategory = null,
            RequestId = requestId,
        };

    /// <summary>
    /// A refused save (US-009 spec FR-009, I-1): the kind of object is named, the identifier is not, because no
    /// connection was created or changed (db-design §4.1).
    /// </summary>
    public static AuditEvent WorkspaceConnectionSaveRefused(
        long appUserId,
        AuditRefusalCategory category,
        DateTimeOffset occurredAt,
        string? requestId)
    {
        if (!SaveRefusals.Contains(category))
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "A refused connection save carries a category of its own action, never one of the sign-in path.");
        }

        return new AuditEvent
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.AppUser,
            ActorId = appUserId,
            ActorRole = AppRole.Admin,
            Action = AuditAction.WorkspaceConnectionSaved,
            TargetType = AuditTargetType.WorkspaceConnection,
            TargetId = null,
            Outcome = AuditOutcome.Refused,
            RefusalCategory = category,
            RequestId = requestId,
        };
    }

    /// <summary>
    /// A carried-out access check (US-011 spec FR-008, I-4; db-design §3.2): <c>succeeded</c> means the check ran,
    /// whatever it found. It names the connection it checked and nothing it found.
    /// </summary>
    public static AuditEvent AccessCheckRun(
        long appUserId,
        long connectionId,
        DateTimeOffset occurredAt,
        string? requestId) =>
        new()
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.AppUser,
            ActorId = appUserId,
            ActorRole = AppRole.Admin,
            Action = AuditAction.AccessCheckRun,
            TargetType = AuditTargetType.WorkspaceConnection,
            TargetId = connectionId,
            Outcome = AuditOutcome.Succeeded,
            RefusalCategory = null,
            RequestId = requestId,
        };

    /// <summary>
    /// A refused access check (US-011 spec FR-008; db-design §3.2): the connection is named when a row exists, and
    /// only the two refusal categories of the check are accepted.
    /// </summary>
    public static AuditEvent AccessCheckRefused(
        long appUserId,
        AuditRefusalCategory category,
        long? connectionId,
        DateTimeOffset occurredAt,
        string? requestId)
    {
        if (category is not (AuditRefusalCategory.ReadOnlyMode or AuditRefusalCategory.ConnectionNotUsable))
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "A refused access check carries a category of its own action.");
        }

        return new AuditEvent
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.AppUser,
            ActorId = appUserId,
            ActorRole = AppRole.Admin,
            Action = AuditAction.AccessCheckRun,
            TargetType = AuditTargetType.WorkspaceConnection,
            TargetId = connectionId,
            Outcome = AuditOutcome.Refused,
            RefusalCategory = category,
            RequestId = requestId,
        };
    }

    /// <summary>
    /// An accepted manual synchronization request (US-019 spec FR-005; entity model §2.2): the actor and their role
    /// from the session, no target.
    /// </summary>
    public static AuditEvent SynchronizationRequested(
        long appUserId,
        AppRole actorRole,
        DateTimeOffset occurredAt,
        string? requestId) =>
        SynchronizationRequestRow(appUserId, actorRole, AuditOutcome.Succeeded, null, occurredAt, requestId);

    /// <summary>
    /// A refused manual synchronization request (US-019 spec FR-005; entity model §2.2): only
    /// <see cref="AuditRefusalCategory.ReadOnlyMode"/> and <see cref="AuditRefusalCategory.ConnectionNotUsable"/>.
    /// </summary>
    public static AuditEvent SynchronizationRequestRefused(
        long appUserId,
        AppRole actorRole,
        AuditRefusalCategory category,
        DateTimeOffset occurredAt,
        string? requestId)
    {
        if (category is not (AuditRefusalCategory.ReadOnlyMode or AuditRefusalCategory.ConnectionNotUsable))
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "A refused synchronization request carries a category of its own action.");
        }

        return SynchronizationRequestRow(appUserId, actorRole, AuditOutcome.Refused, category, occurredAt, requestId);
    }

    private static AuditEvent SynchronizationRequestRow(
        long appUserId,
        AppRole actorRole,
        AuditOutcome outcome,
        AuditRefusalCategory? category,
        DateTimeOffset occurredAt,
        string? requestId)
    {
        if (!Enum.IsDefined(actorRole))
        {
            throw new ArgumentOutOfRangeException(nameof(actorRole), actorRole, "Not a declared role.");
        }

        return new AuditEvent
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.AppUser,
            ActorId = appUserId,
            ActorRole = actorRole,
            Action = AuditAction.SynchronizationRequested,
            TargetType = null,
            TargetId = null,
            Outcome = outcome,
            RefusalCategory = category,
            RequestId = requestId,
        };
    }

    /// <summary>The categories a refused connection save may carry (US-009 db-design §4.2).</summary>
    private static readonly AuditRefusalCategory[] SaveRefusals =
    [
        AuditRefusalCategory.DomainMismatch,
        AuditRefusalCategory.ImpersonationDomainMismatch,
        AuditRefusalCategory.DomainNotConfirmed,
        AuditRefusalCategory.ReadOnlyMode,
    ];

    /// <summary>
    /// A succeeded Dean-account management action by an Admin (US-012 spec FR-017; db-design §4.4). The action
    /// says which of the four it was; the target is always the account acted upon.
    /// </summary>
    public static AuditEvent DeanAccountManaged(
        long adminId,
        AuditAction action,
        long deanId,
        DateTimeOffset occurredAt,
        string? requestId)
    {
        if (action is not (AuditAction.DeanAccountCreated or AuditAction.DeanAccountDisabled
            or AuditAction.DeanAccountReEnabled or AuditAction.DeanAccountPasswordReset))
        {
            throw new ArgumentOutOfRangeException(
                nameof(action),
                action,
                "A Dean-account management row carries one of the four actions SC-11 names.");
        }

        return new AuditEvent
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.AppUser,
            ActorId = adminId,
            ActorRole = AppRole.Admin,
            Action = action,
            TargetType = AuditTargetType.AppUser,
            TargetId = deanId,
            Outcome = AuditOutcome.Succeeded,
            RefusalCategory = null,
            RequestId = requestId,
        };
    }

    /// <summary>
    /// A refused Dean-account management action (US-012 spec FR-017). Only read-only mode refuses one, and the
    /// target is named when a row exists — a refused creation names none (db-design §4.4).
    /// </summary>
    public static AuditEvent DeanAccountManagementRefused(
        long adminId,
        AuditAction action,
        long? deanId,
        AuditRefusalCategory category,
        DateTimeOffset occurredAt,
        string? requestId)
    {
        if (category is not AuditRefusalCategory.ReadOnlyMode)
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "A refused Dean-account action carries the read-only category (US-012 spec FR-015).");
        }

        return new AuditEvent
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.AppUser,
            ActorId = adminId,
            ActorRole = AppRole.Admin,
            Action = action,
            TargetType = deanId is null ? null : AuditTargetType.AppUser,
            TargetId = deanId,
            Outcome = AuditOutcome.Refused,
            RefusalCategory = category,
            RequestId = requestId,
        };
    }

    /// <summary>A Dean changed their own password — the forced change or a later one (US-012 spec FR-017).</summary>
    public static AuditEvent DeanPasswordChanged(long deanId, DateTimeOffset occurredAt, string? requestId) =>
        new()
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.AppUser,
            ActorId = deanId,
            ActorRole = AppRole.Dean,
            Action = AuditAction.DeanPasswordChanged,
            TargetType = AuditTargetType.AppUser,
            TargetId = deanId,
            Outcome = AuditOutcome.Succeeded,
            RefusalCategory = null,
            RequestId = requestId,
        };

    /// <summary>A Dean signed in (US-012 spec FR-017, steps 5 and 6 of FR-012).</summary>
    public static AuditEvent DeanSignInSucceeded(long deanId, DateTimeOffset occurredAt, string? requestId) =>
        new()
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.AppUser,
            ActorId = deanId,
            ActorRole = AppRole.Dean,
            Action = AuditAction.DeanSignIn,
            TargetType = AuditTargetType.AppUser,
            TargetId = deanId,
            Outcome = AuditOutcome.Succeeded,
            RefusalCategory = null,
            RequestId = requestId,
        };

    /// <summary>
    /// A refused Dean sign-in against an existing account (US-012 spec FR-017; db-design §4.4): steps 2, 3 and
    /// 4 of the sequence, each with its own category.
    /// </summary>
    public static AuditEvent DeanSignInRefused(
        long deanId,
        AuditRefusalCategory category,
        DateTimeOffset occurredAt,
        string? requestId)
    {
        if (category is not (AuditRefusalCategory.WrongPassword or AuditRefusalCategory.AccountDisabled
            or AuditRefusalCategory.LockedOut))
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "A refused Dean sign-in against a known account carries one of the three categories of steps 2 to 4.");
        }

        return new AuditEvent
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.AppUser,
            ActorId = deanId,
            ActorRole = AppRole.Dean,
            Action = AuditAction.DeanSignIn,
            TargetType = AuditTargetType.AppUser,
            TargetId = deanId,
            Outcome = AuditOutcome.Refused,
            RefusalCategory = category,
            RequestId = requestId,
        };
    }

    /// <summary>
    /// A report template was created, changed or deleted by an Admin or a Dean (US-027 spec FR-016). The row names the
    /// template by id only: no name, no setting, no mark text (SC-10, SC-11).
    /// </summary>
    public static AuditEvent ReportTemplateWritten(
        long actorId,
        AppRole actorRole,
        AuditAction action,
        long templateId,
        DateTimeOffset occurredAt,
        string? requestId)
    {
        CheckReportTemplateAction(action);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(templateId);
        return ReportTemplateRow(actorId, actorRole, action, templateId, AuditOutcome.Succeeded, null, occurredAt, requestId);
    }

    /// <summary>
    /// A report-template write refused in read-only mode (US-027 spec FR-013, FR-016). The target is the template id
    /// when the reference named one, otherwise null.
    /// </summary>
    public static AuditEvent ReportTemplateWriteRefused(
        long actorId,
        AppRole actorRole,
        AuditAction action,
        long? templateId,
        AuditRefusalCategory category,
        DateTimeOffset occurredAt,
        string? requestId)
    {
        CheckReportTemplateAction(action);
        if (category is not AuditRefusalCategory.ReadOnlyMode)
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "A refused report-template write carries the read-only category (US-027 spec FR-013).");
        }

        return ReportTemplateRow(actorId, actorRole, action, templateId, AuditOutcome.Refused, category, occurredAt, requestId);
    }

    private static void CheckReportTemplateAction(AuditAction action)
    {
        if (action is not (AuditAction.ReportTemplateCreated or AuditAction.ReportTemplateChanged
            or AuditAction.ReportTemplateDeleted))
        {
            throw new ArgumentOutOfRangeException(
                nameof(action),
                action,
                "A report-template row carries one of the three actions of US-027 spec FR-016.");
        }
    }

    private static AuditEvent ReportTemplateRow(
        long actorId,
        AppRole actorRole,
        AuditAction action,
        long? templateId,
        AuditOutcome outcome,
        AuditRefusalCategory? category,
        DateTimeOffset occurredAt,
        string? requestId)
    {
        if (!Enum.IsDefined(actorRole))
        {
            throw new ArgumentOutOfRangeException(nameof(actorRole), actorRole, "Not a declared role.");
        }

        return new AuditEvent
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.AppUser,
            ActorId = actorId,
            ActorRole = actorRole,
            Action = action,
            TargetType = AuditTargetType.ReportTemplate, // db-design §4: always, also with no id
            TargetId = templateId,
            Outcome = outcome,
            RefusalCategory = category,
            RequestId = requestId,
        };
    }

    /// <summary>
    /// US-028 spec FR-010 (entity model §1.1): one row per successful journal export, targeting the exported course. A
    /// null <paramref name="templateId"/> is the built-in template. Only ids, the period, a count and a format code are
    /// recorded — no content and no personal data (SC-10, SC-11). There is no refused form: a failed export writes no row.
    /// </summary>
    public static AuditEvent JournalExported(
        long actorId,
        AppRole actorRole,
        long courseId,
        DateOnly from,
        DateOnly to,
        long? templateId,
        int rows,
        ExportFormat format,
        DateTimeOffset occurredAt,
        string? requestId)
    {
        if (!Enum.IsDefined(actorRole))
        {
            throw new ArgumentOutOfRangeException(nameof(actorRole), actorRole, "Not a declared role.");
        }

        if (!Enum.IsDefined(format))
        {
            throw new ArgumentOutOfRangeException(nameof(format), format, "Not a declared export format.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(courseId);
        if (templateId is { } id)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id, nameof(templateId));
        }

        if (from > to)
        {
            throw new ArgumentOutOfRangeException(nameof(from), from, "The exported period begins after it ends.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(rows);

        return new AuditEvent
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.AppUser,
            ActorId = actorId,
            ActorRole = actorRole,
            Action = AuditAction.JournalExported,
            TargetType = AuditTargetType.Course,
            TargetId = courseId,
            Outcome = AuditOutcome.Succeeded,
            RefusalCategory = null,
            RequestId = requestId,
            ExportPeriodFrom = from,
            ExportPeriodTo = to,
            ExportTemplateId = templateId,
            ExportTemplateBuiltIn = templateId is null,
            ExportRows = rows,
            ExportFormat = format,
        };
    }

    /// <summary>
    /// Step 1 of the sequence: no account matched, so the row names no actor and no target at all — the typed
    /// login is never recorded (US-012 spec FR-017, SC-11; db-design §4.4).
    /// </summary>
    public static AuditEvent DeanSignInRefusedUnknownLogin(DateTimeOffset occurredAt, string? requestId) =>
        new()
        {
            OccurredAt = occurredAt,
            ActorType = AuditActorType.Anonymous,
            ActorId = null,
            ActorRole = null,
            Action = AuditAction.DeanSignIn,
            TargetType = null,
            TargetId = null,
            Outcome = AuditOutcome.Refused,
            RefusalCategory = AuditRefusalCategory.UnknownLogin,
            RequestId = requestId,
        };
}
