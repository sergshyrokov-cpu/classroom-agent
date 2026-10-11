namespace ClassroomAgent.Application.Models;

/// <summary>Per lower-cased email: the displayed name (tie rule, db-design §4) and whether it is a personal account (BR-064).</summary>
public sealed record MeetReportPersonRecord(string LowerCaseEmail, string? FullName, bool HasAnyMembership);
