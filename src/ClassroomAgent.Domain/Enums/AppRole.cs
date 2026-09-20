namespace ClassroomAgent.Domain.Enums;

/// <summary>
/// The application role of an installation account (US-008 entity model §2.3; <c>trebovaniya.md</c> §2).
/// Exactly two members in the first version: Teacher and Student are Epic 7 and exist only as synced data.
/// </summary>
public enum AppRole
{
    Admin,
    Dean,
}
