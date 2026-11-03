namespace ClassroomAgent.Application.Models;

/// <summary>US-071 entity model §2: a student the header search found.</summary>
public sealed record StudentSearchRecord(long ParticipantId, string? Surname, string? GivenName, string? Email);
