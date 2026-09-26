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
}
