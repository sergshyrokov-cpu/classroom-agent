namespace ClassroomAgent.Application.Models;

/// <summary>One organizer of a Meet code as stored (US-074 db-design §4): the email and the chosen participant name
/// before the blank test, null when no participant of that email has a name.</summary>
public sealed record MeetOrganizerRecord(string Email, string? StoredFullName);
