using System.Globalization;
using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The synthetic Meet world of US-031's tests (TC-4): invented conferences, endpoints, codes and accounts of the test
/// school's domain, and the names the tests fix for IMPLEMENTATION — log events, translation keys, constraints. Nothing
/// here comes from a real domain or a real meeting.
/// </summary>
public static class MeetTestData
{
    /// <summary>The school's domain — the saved <c>WorkspaceConnection</c> domain of every test world.</summary>
    public const string SchoolDomain = InstallationTestData.Domain;

    /// <summary>A subdomain of the school's domain: not a domain account (spec FR-006, OD-005).</summary>
    public const string Subdomain = "sub." + InstallationTestData.Domain;

    /// <summary>Another school's domain.</summary>
    public const string OtherDomain = InstallationTestData.OtherDomain;

    /// <summary>
    /// The <c>identifier_type</c> value Google documents for an email identifier (spec FR-003, §7 item 28 — unverified
    /// on a live domain, OD-009). Any other value makes the connection an "other participant".
    /// </summary>
    public const string EmailIdentifierType = "email_address";

    /// <summary>A non-email identifier type, for a phone dial-in.</summary>
    public const string PhoneIdentifierType = "phone_number";

    /// <summary>180 days back, plus the hour of spec I-1.</summary>
    public static readonly TimeSpan Horizon = TimeSpan.FromDays(180) - TimeSpan.FromHours(1);

    /// <summary>The overlap of a later pull (spec FR-002, OD-003).</summary>
    public static readonly TimeSpan Overlap = TimeSpan.FromDays(3);

    public static string ConferenceId(int n) => $"conf-{n:D4}-synthetic";

    public static string MeetingCode(int n) => $"abc-{n:D4}-xyz";

    public static string EndpointId(int n) => $"endpoint-{n:D4}";

    /// <summary>An account of the school's domain.</summary>
    public static string DomainEmail(string local) => $"{local}@{SchoolDomain}";

    public static string Teacher(int n) => DomainEmail($"teacher{n}");

    public static string Student(int n) => DomainEmail($"student{n}");

    /// <summary>
    /// One <c>call_ended</c> event: a connection of <paramref name="conference"/> that left at
    /// <paramref name="leftAt"/> after <paramref name="durationSeconds"/>, by <paramref name="participant"/> (an email,
    /// with the email identifier type unless <paramref name="identifierType"/> says otherwise).
    /// </summary>
    public static MeetCallEndedEvent Event(
        int conference,
        int endpoint,
        DateTimeOffset leftAt,
        int durationSeconds,
        string? organizer,
        string? participant = null,
        string? identifierType = EmailIdentifierType,
        string? meetingCode = null) =>
        new(
            leftAt,
            ConferenceId(conference),
            meetingCode ?? MeetingCode(conference),
            organizer,
            EndpointId(endpoint),
            participant,
            participant is null ? null : identifierType,
            durationSeconds);

    /// <summary>The log events the tests fix for IMPLEMENTATION (spec FR-012 names none).</summary>
    public static class LogEvents
    {
        /// <summary>Information: the Meet step finished, with the window and the counts.</summary>
        public const string StepCompleted = "SyncMeetStepCompleted";

        /// <summary>Warning: invalid events were skipped in this run, a count per VR-001 reason.</summary>
        public const string EventsSkipped = "SyncMeetEventsSkipped";
    }

    /// <summary>
    /// The structured properties of <see cref="LogEvents.StepCompleted"/>: the window and the counts of spec FR-012,
    /// named after <see cref="MeetPullCounts"/>.
    /// </summary>
    public static class LogProperties
    {
        public const string RunId = "RunId";

        public const string WindowFrom = "WindowFrom";

        public const string WindowTo = "WindowTo";

        public const string EventsRead = "EventsRead";

        public const string SessionsAdded = "SessionsAdded";

        public const string SessionsUpdated = "SessionsUpdated";

        public const string ParticipationsAdded = "ParticipationsAdded";

        public const string ParticipationsUpdated = "ParticipationsUpdated";

        public const string NotOfTheSchool = "NotOfTheSchool";

        public const string Skipped = "Skipped";

        /// <summary>On the existing <c>SyncRunFailed</c> line: the step that stopped the run (spec FR-010, FR-012).</summary>
        public const string Step = "Step";

        /// <summary>On <see cref="LogEvents.EventsSkipped"/>: the VR-001 reason and its count.</summary>
        public const string Reason = "Reason";

        public const string Count = "Count";
    }

    /// <summary>The translation keys of spec FR-014, fixed here (the API design leaves key names to the tests).</summary>
    public static class Keys
    {
        /// <summary>"Meet meetings loaded up to:" — the date and time follow it.</summary>
        public const string LoadedUpTo = "LastSync.Meet.LoadedUpTo";

        /// <summary>"Meet meetings: not loaded yet".</summary>
        public const string NotLoaded = "LastSync.Meet.NotLoaded";

        /// <summary>The step that stopped a failed run: the Classroom read.</summary>
        public const string StepClassroom = "LastSync.Step.Classroom";

        /// <summary>The step that stopped a failed run: the Meet events read.</summary>
        public const string StepMeet = "LastSync.Step.Meet";

        public static readonly string[] All = [LoadedUpTo, NotLoaded, StepClassroom, StepMeet];
    }

    /// <summary>The tables, columns, constraints and indexes of db-design §2 … §5.</summary>
    public static class Names
    {
        public const string SessionTable = "meet_session";

        public const string ParticipationTable = "meet_participation";

        public const string SessionConferenceUnique = "uq_meet_session_conference_id";

        public const string SessionEndedAfterStarted = "ck_meet_session_ended_after_started";

        public const string SessionValues = "ck_meet_session_values";

        public const string SessionStartedAtIndex = "ix_meet_session_started_at";

        public const string SessionCodeStartedAtIndex = "ix_meet_session_meeting_code_started_at";

        public const string ParticipationSessionForeignKey = "fk_meet_participation_meet_session_id";

        public const string ParticipationEndpointUnique = "uq_meet_participation_meet_session_id_endpoint_id";

        public const string ParticipationDuration = "ck_meet_participation_duration";

        public const string ParticipationValues = "ck_meet_participation_values";

        public const string ParticipationEmailJoinedAtIndex = "ix_meet_participation_email_joined_at";

        public const string SyncFailedStep = "ck_sync_state_failed_step";

        public const string SyncMeetLoadedUpTo = "ck_sync_state_meet_loaded_up_to";

        public const string AuditMeetCounts = "ck_audit_event_purge_meet_counts";

        public const string AuditMeetCountsAbsent = "ck_audit_event_purge_meet_counts_absent";

        public const string AuditMeetCountsNonNegative = "ck_audit_event_purge_meet_counts_non_negative";
    }

    /// <summary>
    /// The watermark as the block shows it (spec FR-011, OD-010 a): a local date-time of the school's time zone in the
    /// request culture's short date and <c>HH:mm</c>, with no "UTC" suffix.
    /// </summary>
    public static string FormatLocal(DateTime local, string culture)
    {
        var info = CultureInfo.GetCultureInfo(culture);
        return local.ToString(info.DateTimeFormat.ShortDatePattern + " HH:mm", info);
    }
}
