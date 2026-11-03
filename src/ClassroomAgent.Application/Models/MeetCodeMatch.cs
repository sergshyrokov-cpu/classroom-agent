namespace ClassroomAgent.Application.Models;

/// <summary>US-071 entity model §2: a Meet code the header search found, with the list that holds it.</summary>
public sealed record MeetCodeMatch(string Code, MeetCodeList List);
