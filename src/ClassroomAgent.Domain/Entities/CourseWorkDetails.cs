namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// A coursework or material item's fields other than its identity (US-015 entity model §1.1), mirroring how
/// <c>CourseDetails</c> splits identity from details (US-014 entity model §1.2). It lives in <c>Domain</c> because
/// <see cref="CourseWork"/> takes it and <c>Domain</c> references nothing (entity model §1.2, AD-3).
/// </summary>
/// <param name="Title">The item's title; truncated at <see cref="CourseWork.MaxTitleLength"/>.</param>
/// <param name="ItemDate">The one date of the FR-008 cascade.</param>
/// <param name="DueAt">Absent unless Google set both date and time (db-design §3.4).</param>
/// <param name="MaxPoints">Absent means ungraded work.</param>
/// <param name="CreationTime">Google's creation time, stored as given.</param>
/// <param name="UpdateTime">Google's update time; PC-11 reads it.</param>
public sealed record CourseWorkDetails(
    string Title,
    DateTimeOffset ItemDate,
    DateTimeOffset? DueAt,
    decimal? MaxPoints,
    DateTimeOffset? CreationTime,
    DateTimeOffset? UpdateTime);
