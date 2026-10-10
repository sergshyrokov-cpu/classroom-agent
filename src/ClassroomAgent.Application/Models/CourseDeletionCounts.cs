namespace ClassroomAgent.Application.Models;

/// <summary>What deleting one expired course removed beyond the course itself (US-032 db-design §6).</summary>
public sealed record CourseDeletionCounts(int MeetSessions, int MeetParticipations, int MeetCodeLinks);
