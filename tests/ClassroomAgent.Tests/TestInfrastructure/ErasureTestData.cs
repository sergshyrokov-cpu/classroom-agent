using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-038 fixed names: the paths, policy and form fields of docs/designs/api/US-038-openapi.yaml; the markup hooks,
/// translation keys and log event of docs/tests/US-038-test-strategy.md §4–§6, which IMPLEMENTATION renders exactly;
/// the table, column and constraint names of docs/designs/database/US-038-db-design.md.
/// </summary>
public static partial class ErasureTestData
{
    /// <summary>openapi <c>GET /settings/erasure</c>.</summary>
    public const string PagePath = "/settings/erasure";

    /// <summary>openapi <c>POST /settings/erasure/searches</c>.</summary>
    public const string SearchesPath = "/settings/erasure/searches";

    /// <summary>openapi <c>POST /settings/erasure/exclusion-liftings</c>.</summary>
    public const string LiftingsPath = "/settings/erasure/exclusion-liftings";

    /// <summary>US-065 openapi <c>POST /settings/erasure/exclusions</c>.</summary>
    public const string EntriesPath = "/settings/erasure/exclusions";

    /// <summary>openapi <c>GET /settings/erasure/participants/{participantId}</c>.</summary>
    public static string ConfirmationPath(long participantId) =>
        "/settings/erasure/participants/" + participantId.ToString(CultureInfo.InvariantCulture);

    /// <summary>The same path with a raw id segment, for the malformed-id cases (VR-005).</summary>
    public static string ConfirmationPath(string rawId) => "/settings/erasure/participants/" + rawId;

    /// <summary>openapi <c>POST /settings/erasure/participants/{participantId}/erasure</c>.</summary>
    public static string ErasurePath(long participantId) => ConfirmationPath(participantId) + "/erasure";

    /// <summary>The new policy of spec FR-015 (api-design §2.7).</summary>
    public const string Policy = "EraseParticipantData";

    /// <summary>The route patterns as <see cref="HostEndpoint.Pattern"/> normalizes them (constraint stripped, lower case).</summary>
    public static class Patterns
    {
        public const string Page = "settings/erasure";
        public const string Searches = "settings/erasure/searches";
        public const string Confirmation = "settings/erasure/participants/{participantid}";
        public const string Erasure = "settings/erasure/participants/{participantid}/erasure";
        public const string Liftings = "settings/erasure/exclusion-liftings";

        /// <summary>US-065: the entry of an exclusion in advance (not in <see cref="All"/>, which is US-038's five).</summary>
        public const string Entries = "settings/erasure/exclusions";

        public static IReadOnlyList<string> All { get; } = [Page, Searches, Confirmation, Erasure, Liftings];
    }

    /// <summary>The form field names of api-design §2.8.</summary>
    public static class Fields
    {
        public const string Term = "term";
        public const string Mark = "irreversibleConfirmed";
        public const string Email = "email";
    }

    /// <summary>The operation names the read-only guard records (the use cases' <c>Operation</c> constants).</summary>
    public static class Operations
    {
        public const string Erase = "Participant.Erase";
        public const string Lift = "ErasureExclusion.Lift";
        public const string Enter = "ErasureExclusion.Enter";
    }

    /// <summary>Markup hooks (strategy §4).</summary>
    public static class Markup
    {
        /// <summary>The home page's Admin block, which gains the link to <see cref="PagePath"/>.</summary>
        public const string AdminSettings = "admin-settings";

        /// <summary>The form posting to <see cref="SearchesPath"/>.</summary>
        public const string SearchForm = "erasure-search-form";

        /// <summary>The form posting to <see cref="LiftingsPath"/>.</summary>
        public const string LiftForm = "erasure-lift-form";

        /// <summary>The list of search results; each result's element carries <see cref="ResultAttribute"/>.</summary>
        public const string Results = "erasure-results";

        /// <summary><c>data-participant-id="&lt;id&gt;"</c> on each result, which links to its confirmation page.</summary>
        public const string ResultAttribute = "data-participant-id";

        /// <summary>The "more than 50 matched, refine the term" message.</summary>
        public const string Truncated = "erasure-truncated";

        /// <summary>The "nobody found" message.</summary>
        public const string NoneFound = "erasure-none-found";

        /// <summary>The VR-001 field error of the search form.</summary>
        public const string SearchError = "erasure-search-error";

        /// <summary>The VR-003 field error of the lifting form.</summary>
        public const string LiftError = "erasure-lift-error";

        /// <summary>The "no exclusion for this address" outcome.</summary>
        public const string NoExclusion = "erasure-no-exclusion";

        /// <summary>The read-only notice on the page and the confirmation (spec FR-014, I-8).</summary>
        public const string ReadOnlyNotice = "erasure-read-only-notice";

        /// <summary>The one-time message after a redirect.</summary>
        public const string Message = "erasure-message";

        /// <summary>The confirmation page's main element.</summary>
        public const string Confirmation = "erasure-confirmation";

        /// <summary>The list of the participant's courses; one <c>li</c> per membership.</summary>
        public const string Courses = "erasure-courses";

        /// <summary>The element whose only text is a count's number carries <c>data-erasure-count="&lt;name&gt;"</c>.</summary>
        public const string CountAttribute = "data-erasure-count";

        /// <summary>The warning that files already exported are outside the program.</summary>
        public const string ExportWarning = "erasure-export-warning";

        /// <summary>The OD-001 notice for a participant with no email.</summary>
        public const string NoEmailNotice = "erasure-no-email-notice";

        /// <summary>The VR-002 error next to the mark.</summary>
        public const string MarkError = "erasure-mark-error";

        /// <summary>The form posting to the erasure; its submit button is <c>disabled</c> in read-only mode.</summary>
        public const string EraseForm = "erasure-form";

        /// <summary>US-065: the form posting to <see cref="EntriesPath"/>; its submit button is <c>disabled</c> in read-only mode.</summary>
        public const string EnterForm = "erasure-enter-form";

        /// <summary>US-065: the VR-001 field error of the entry form.</summary>
        public const string EnterError = "erasure-enter-error";

        /// <summary>US-065: the "this person is already loaded — erase their data the ordinary way" message.</summary>
        public const string AlreadyLoaded = "erasure-already-loaded";

        /// <summary>
        /// US-065: the list of the participants already loaded with that email; each entry carries
        /// <see cref="ResultAttribute"/> and links to its confirmation page.
        /// </summary>
        public const string LoadedResults = "erasure-loaded-results";

        /// <summary>US-065: more than 50 participants have that email; the list shows 50.</summary>
        public const string LoadedTruncated = "erasure-loaded-truncated";

        /// <summary>US-065: the "this email is already excluded" message.</summary>
        public const string AlreadyExcluded = "erasure-already-excluded";
    }

    /// <summary>The values of <see cref="Markup.CountAttribute"/>.</summary>
    public static class Counts
    {
        public const string Submissions = "submissions";
        public const string Grades = "grades";
        public const string MeetParticipations = "meet-participations";
        public const string OrganizedMeetings = "organized-meetings";
        public const string OwnedCourses = "owned-courses";

        public static IReadOnlyList<string> All { get; } =
            [Submissions, Grades, MeetParticipations, OrganizedMeetings, OwnedCourses];
    }

    /// <summary>Translation keys (strategy §5).</summary>
    public static class Keys
    {
        public const string Title = "Erasure.Title";
        public const string NavigationEntry = "Erasure.NavigationEntry";
        public const string SearchLabel = "Erasure.Search.Label";
        public const string SearchButton = "Erasure.Search.Button";
        public const string NoneFound = "Erasure.Search.NoneFound";
        public const string Truncated = "Erasure.Search.Truncated";
        public const string NoName = "Erasure.Search.NoName";
        public const string NoEmail = "Erasure.Search.NoEmail";
        public const string TeacherCourses = "Erasure.Search.TeacherCourses";
        public const string StudentCourses = "Erasure.Search.StudentCourses";
        public const string TermRequired = "Erasure.Search.Error.TermRequired";
        public const string TermTooShort = "Erasure.Search.Error.TermTooShort";
        public const string TermTooLong = "Erasure.Search.Error.TermTooLong";
        public const string LiftTitle = "Erasure.Lift.Title";
        public const string LiftLabel = "Erasure.Lift.Label";
        public const string LiftButton = "Erasure.Lift.Button";
        public const string NoExclusion = "Erasure.Lift.NoExclusion";
        public const string EmailRequired = "Erasure.Lift.Error.EmailRequired";
        public const string EmailTooLong = "Erasure.Lift.Error.EmailTooLong";
        public const string EmailInvalid = "Erasure.Lift.Error.EmailInvalid";
        public const string Erased = "Erasure.Message.Erased";
        public const string ExclusionLifted = "Erasure.Message.ExclusionLifted";
        public const string ReadOnlyNotice = "Erasure.ReadOnlyNotice";
        public const string ConfirmationTitle = "Erasure.Confirmation.Title";
        public const string Courses = "Erasure.Confirmation.Courses";
        public const string Submissions = "Erasure.Confirmation.Submissions";
        public const string Grades = "Erasure.Confirmation.Grades";
        public const string MeetParticipations = "Erasure.Confirmation.MeetParticipations";
        public const string OrganizedMeetings = "Erasure.Confirmation.OrganizedMeetings";
        public const string OwnedCourses = "Erasure.Confirmation.OwnedCourses";
        public const string ExportWarning = "Erasure.Confirmation.ExportWarning";
        public const string NoEmailNotice = "Erasure.Confirmation.NoEmailNotice";
        public const string Mark = "Erasure.Confirmation.Mark";
        public const string EraseButton = "Erasure.Confirmation.Button";
        public const string MarkRequired = "Erasure.Confirmation.MarkRequired";
        public const string RoleTeacher = "Erasure.Role.Teacher";
        public const string RoleStudent = "Erasure.Role.Student";

        // US-065 (test strategy §5). The entry's field errors reuse the three Erasure.Lift.Error.* keys (one shared VR-001).
        public const string EnterTitle = "Erasure.Enter.Title";
        public const string EnterExplanation = "Erasure.Enter.Explanation";
        public const string EnterLabel = "Erasure.Enter.Label";
        public const string EnterButton = "Erasure.Enter.Button";
        public const string AlreadyLoaded = "Erasure.Enter.AlreadyLoaded";
        public const string AlreadyExcluded = "Erasure.Enter.AlreadyExcluded";
        public const string LoadedTruncated = "Erasure.Enter.Truncated";
        public const string ExclusionEntered = "Erasure.Message.ExclusionEntered";

        /// <summary>US-065's keys.</summary>
        public static IReadOnlyList<string> Entry { get; } =
        [
            EnterTitle, EnterExplanation, EnterLabel, EnterButton, AlreadyLoaded, AlreadyExcluded, LoadedTruncated,
            ExclusionEntered,
        ];

        public static IReadOnlyList<string> All { get; } =
        [
            Title, NavigationEntry, SearchLabel, SearchButton, NoneFound, Truncated, NoName, NoEmail, TeacherCourses,
            StudentCourses, TermRequired, TermTooShort, TermTooLong, LiftTitle, LiftLabel, LiftButton, NoExclusion,
            EmailRequired, EmailTooLong, EmailInvalid, Erased, ExclusionLifted, ReadOnlyNotice, ConfirmationTitle,
            Courses, Submissions, Grades, MeetParticipations, OrganizedMeetings, OwnedCourses, ExportWarning,
            NoEmailNotice, Mark, EraseButton, MarkRequired, RoleTeacher, RoleStudent,
        ];
    }

    /// <summary>The log event of spec FR-013 (strategy §6): one <c>Information</c> line per erasure, after commit.</summary>
    public static class Log
    {
        public const string Erased = "ParticipantErased";

        public const string ParticipantId = "ParticipantId";
        public const string Memberships = "Memberships";
        public const string Submissions = "Submissions";
        public const string MeetParticipations = "MeetParticipations";
        public const string ClearedMeetOrganizers = "ClearedMeetOrganizers";
        public const string ClearedCourseOwners = "ClearedCourseOwners";

        public static IReadOnlyList<string> CountProperties { get; } =
            [Memberships, Submissions, MeetParticipations, ClearedMeetOrganizers, ClearedCourseOwners];
    }

    /// <summary>The stored codes of db-design §9 and the existing ones it reuses.</summary>
    public static class Codes
    {
        public const string ErasedAction = "participant_erased";
        public const string LiftedAction = "erasure_exclusion_lifted";
        public const string EnteredAction = "erasure_exclusion_entered";
        public const string ParticipantTarget = "classroom_participant";
        public const string ExclusionTarget = "erased_participant";
        public const string Succeeded = "succeeded";
        public const string Refused = "refused";
        public const string ReadOnlyCategory = "read_only_mode";
        public const string AppUserActor = "app_user";
        public const string AdminRole = "admin";
    }

    /// <summary>db-design table, column, constraint and index names.</summary>
    public static class Names
    {
        public const string ExclusionTable = "erased_participant";
        public const string SaltTable = "erasure_salt";
        public const string ExclusionGoogleUserIdUnique = "uq_erased_participant_google_user_id";
        public const string ExclusionValues = "ck_erased_participant_values";
        public const string ExclusionImmutable = "ck_erased_participant_immutable";
        public const string ExclusionFingerprintIndex = "ix_erased_participant_email_fingerprint";
        public const string SaltSingleton = "ck_erasure_salt_singleton";
        public const string SaltSingletonUnique = "uq_erasure_salt_singleton";
        public const string SaltValues = "ck_erasure_salt_values";
        public const string SaltImmutable = "ck_erasure_salt_immutable";
        public const string SessionOrganizerIndex = "ix_meet_session_organizer_email_lower";
        public const string ParticipationEmailIndex = "ix_meet_participation_email_lower";
        public const string CourseOwnerIndex = "ix_course_owner_google_id";
        public const string AuditErasureCounts = "ck_audit_event_erasure_counts";
        public const string AuditErasureCountsAbsent = "ck_audit_event_erasure_counts_absent";
        public const string AuditErasureCountsNonNegative = "ck_audit_event_erasure_counts_non_negative";
        public const string AuditErasureShape = "ck_audit_event_erasure_shape";
        public const string AuditLiftShape = "ck_audit_event_exclusion_lift_shape";
        public const string Migration = "_AddParticipantErasure";

        // US-065 db-design §2.3, §6.2, §7.
        public const string FillOnlyTrigger = "tg_erased_participant_fill_only";
        public const string FillOnlyFunction = "erased_participant_fill_only";
        public const string AuditEntryShape = "ck_audit_event_exclusion_entry_shape";
        public const string AuditAction = "ck_audit_event_action";
        public const string InAdvanceMigration = "_AddErasureExclusionInAdvance";

        public static IReadOnlyList<string> AuditCountColumns { get; } =
        [
            "erased_memberships", "erased_submissions", "erased_meet_participations", "cleared_meet_organizers",
            "cleared_course_owners",
        ];
    }

    /// <summary>db-design §7.3: the advisory-lock key of the write fence (<c>ErasureFenceKey</c>).</summary>
    public const long FenceKey = 0x4552415345L;

    /// <summary>The salt the tests seed when they need a known one: 32 bytes 0x01 … 0x20.</summary>
    public static byte[] KnownSalt() => Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

    /// <summary>
    /// The fingerprint computed independently of the code under test (spec FR-010, VR-004): HMAC-SHA-256 keyed with
    /// the salt over UTF-8 of the email trimmed and lower-cased. The test emails have plain ASCII domains, so the
    /// domain normalization of <c>WorkspaceConnection.NormalizeEmail</c> adds nothing to that.
    /// </summary>
    public static byte[] Fingerprint(string email, byte[] salt) =>
        HMACSHA256.HashData(salt, Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));

    /// <summary>The number shown by the count <paramref name="name"/>; null when the page has no such count.</summary>
    public static int? Count(string html, string name)
    {
        var match = Regex.Match(
            html,
            @"<[a-z0-9]+[^>]*\b" + Markup.CountAttribute + @"=""" + Regex.Escape(name) + @"""[^>]*>\s*(?<n>[0-9]+)\s*<",
            RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>The participant ids of the search results, in page order.</summary>
    public static IReadOnlyList<long> ResultIds(string html) =>
        Regex.Matches(html, @"\b" + Markup.ResultAttribute + @"=""(?<id>[0-9]+)""", RegexOptions.IgnoreCase)
            .Select(m => long.Parse(m.Groups["id"].Value, CultureInfo.InvariantCulture))
            .ToList();

    /// <summary>Whether an element with that id is on the page.</summary>
    public static bool HasElement(string html, string id) =>
        html.Contains("id=\"" + id + "\"", StringComparison.Ordinal);

    /// <summary>The inner HTML of the element with that id (its own tag name closes it); null when absent.</summary>
    public static string? Inner(string html, string id)
    {
        var match = Regex.Match(
            html,
            @"<(?<tag>[a-z0-9]+)\b[^>]*\sid=""" + Regex.Escape(id) + @"""[^>]*>(?<inner>.*?)</\k<tag>>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups["inner"].Value : null;
    }
}
