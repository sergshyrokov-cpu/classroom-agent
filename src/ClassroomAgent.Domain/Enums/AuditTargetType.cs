namespace ClassroomAgent.Domain.Enums;

/// <summary>
/// What an audited action acted upon. Deliberately empty in US-008: the two sign-in rows have no target, and
/// a member is added by the Story that first needs one (entity model §2.3; spec I-11).
/// </summary>
public enum AuditTargetType
{
}
