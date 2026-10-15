namespace ClassroomAgent.Domain.Enums;

/// <summary>
/// What an audited action acted upon. US-008 left it empty and recorded that the Story adding the first target
/// would fill it; US-009 is that Story (US-008 entity model §2.3, spec I-11).
/// </summary>
public enum AuditTargetType
{
    /// <summary>The installation's connection to its Workspace domain (US-009 db-design §4).</summary>
    WorkspaceConnection,

    /// <summary>An account of the installation — the target of every US-012 action (db-design §4.2).</summary>
    AppUser,

    /// <summary>A report template created in the school (US-027 db-design §4).</summary>
    ReportTemplate,

    /// <summary>US-028 db-design §3.2: the course whose journal was exported.</summary>
    Course,

    /// <summary>US-038 db-design §9.1: the erased participant — a bare id, the row no longer exists by design.</summary>
    ClassroomParticipant,

    /// <summary>US-038 db-design §9.1: the lifted exclusion — a bare id, the row no longer exists by design.</summary>
    ErasedParticipant,
}
