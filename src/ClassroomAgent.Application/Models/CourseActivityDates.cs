namespace ClassroomAgent.Application.Models;

/// <summary>
/// One stored course's own dates and the maxima of its children's (US-037 entity model §3, db-design §4). Raw values:
/// combining them is <see cref="Domain.Rules.RetentionRule"/>'s job, never the store's. US-032 spec FR-016 adds the start
/// of the latest meeting reached through the course's linked codes.
/// </summary>
public sealed record CourseActivityDates(
    long CourseId,
    DateTimeOffset? CourseUpdateTime,
    DateTimeOffset CourseCreatedAt,
    DateTimeOffset? LatestItemCreationTime,
    DateTimeOffset? LatestItemUpdateTime,
    DateTimeOffset? LatestSubmissionUpdateTime,
    DateTimeOffset? LatestLinkedMeetingStart = null);
