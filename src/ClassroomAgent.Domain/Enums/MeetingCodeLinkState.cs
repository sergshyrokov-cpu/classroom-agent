namespace ClassroomAgent.Domain.Enums;

/// <summary>The two states a <c>meeting_code_link</c> row can hold (US-032 db-design §2); an unassigned code has no row.</summary>
public enum MeetingCodeLinkState
{
    Linked,
    Marked,
}
