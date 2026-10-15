namespace ClassroomAgent.Web.Security;

/// <summary>
/// The log line of an erasure (US-038 spec FR-013, BR-077: the Owner counts these lines after a restore; DC-10). The
/// participant's internal id and the counts only — never a name, an email, a Google user id, a fingerprint, the search
/// term or the email to lift (SC-10, S-06).
/// </summary>
public static partial class ErasureLog
{
    [LoggerMessage(
        EventId = 5380,
        EventName = "ParticipantErased",
        Level = LogLevel.Information,
        Message = "Participant {ParticipantId} erased by account {AppUserId}: {Memberships} memberships, {Submissions} submissions, {MeetParticipations} Meet participations deleted; {ClearedMeetOrganizers} meeting organizers, {ClearedCourseOwners} course owners cleared")]
    public static partial void ParticipantErased(
        ILogger logger,
        long participantId,
        long appUserId,
        int memberships,
        int submissions,
        int meetParticipations,
        int clearedMeetOrganizers,
        int clearedCourseOwners);
}
