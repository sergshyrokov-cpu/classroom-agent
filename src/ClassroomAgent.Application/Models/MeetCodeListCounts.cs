namespace ClassroomAgent.Application.Models;

/// <summary>How many codes each list of the Meet meetings page holds (US-032 api-design §2.2).</summary>
public sealed record MeetCodeListCounts(int Unassigned, int Linked, int NotACourse);
