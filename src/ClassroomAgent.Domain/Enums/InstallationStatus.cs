namespace ClassroomAgent.Domain.Enums;

/// <summary>Last known status of the school's <c>Installation</c>, stored as <c>active</c> / <c>suspended</c> (US-005 entity model §3.2).</summary>
public enum InstallationStatus
{
    Active,
    Suspended,
}
