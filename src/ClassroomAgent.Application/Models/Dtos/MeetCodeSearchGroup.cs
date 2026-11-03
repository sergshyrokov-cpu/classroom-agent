namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-071 entity model §3: the Meet codes group; the total is the sum of the three list counters.</summary>
public sealed record MeetCodeSearchGroup(
    IReadOnlyList<MeetCodeMatch> Rows, int Total, MeetCodeList? FirstListWithMatch);
