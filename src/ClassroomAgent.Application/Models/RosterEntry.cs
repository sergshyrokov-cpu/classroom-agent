namespace ClassroomAgent.Application.Models;

/// <summary>
/// One entry of a Classroom course roster, as Classroom gave it (US-014 entity model §6, OD-006).
/// </summary>
/// <param name="GoogleUserId">Google's <c>userId</c>; the person's only identity (OD-011).</param>
/// <param name="Email">The person's address, or null when Classroom did not return one (OD-006).</param>
/// <param name="FullName">The person's name, as one string (I-2).</param>
public sealed record RosterEntry(string GoogleUserId, string? Email, string? FullName);
