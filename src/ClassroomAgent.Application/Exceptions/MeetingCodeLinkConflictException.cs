namespace ClassroomAgent.Application.Exceptions;

/// <summary>
/// A concurrent change of the same code (US-032 db-design §7): thrown by the unit of work for a concurrency-stamp
/// mismatch on <c>meeting_code_link</c> or for <c>uq_meeting_code_link_meeting_code</c>, so <c>Application</c> never
/// sees a provider type. The use cases answer it as a stale state (spec FR-013).
/// </summary>
public sealed class MeetingCodeLinkConflictException : Exception
{
    public MeetingCodeLinkConflictException()
    {
    }

    public MeetingCodeLinkConflictException(Exception innerException)
        : base("The meeting code link was changed concurrently.", innerException)
    {
    }
}
