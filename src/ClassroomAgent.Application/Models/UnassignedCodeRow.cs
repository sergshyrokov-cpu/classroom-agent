namespace ClassroomAgent.Application.Models;

/// <summary>A code with stored meetings and no link row (US-032 db-design §5.1). <c>DomainAccounts</c> counts distinct participant emails, case-insensitively, excluding every organizer of the code; <c>OtherParticipants</c> counts connections without an email (spec I-6). <c>HasCandidate</c> is the SQL test of db-design §5.2.</summary>
public sealed record UnassignedCodeRow(
    string MeetingCode,
    IReadOnlyList<string> OrganizerEmails,
    DateTimeOffset FirstStartedAt,
    DateTimeOffset LastStartedAt,
    int MeetingCount,
    int DomainAccounts,
    int OtherParticipants,
    bool HasCandidate);
