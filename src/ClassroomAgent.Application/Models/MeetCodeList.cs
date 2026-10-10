namespace ClassroomAgent.Application.Models;

/// <summary>The three lists of the Meet meetings page (US-032 api-design §2.2): query values <c>unassigned</c>, <c>linked</c>, <c>not-a-course</c>.</summary>
public enum MeetCodeList
{
    Unassigned,
    Linked,
    NotACourse,
}
