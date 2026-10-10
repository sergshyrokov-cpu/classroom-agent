namespace ClassroomAgent.Application.Models;

/// <summary>What one course's leaver expiry removed (US-037 spec FR-005; US-032 db-design §6).</summary>
public sealed record LeaverDeletionCounts(int Memberships, int MeetParticipations);
