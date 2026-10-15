using System.Globalization;
using ClassroomAgent.Application.MeetLinking;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The classification and arithmetic shared by the Meet reports (US-033 spec FR-005, FR-006, FR-011, I-4, I-8, I-10):
/// a pure function of the read records of entity model §4, the meeting's start date in the school's zone and the
/// course's roster on that date. Emails are compared lower-cased (PC-12).
/// </summary>
internal static class MeetReportRules
{
    /// <summary>One identified person of one meeting: their email as Google gave it, their mark and capped seconds.</summary>
    internal sealed record PersonLine(string LowerCaseEmail, string Email, MeetParticipantMark Mark, long Seconds);

    /// <summary>One meeting classified against the course on its start date (spec FR-005, FR-006, FR-008).</summary>
    internal sealed record MeetingFacts(
        MeetReportMeetingRecord Meeting,
        long DurationSeconds,
        bool OrganizerNotCourseTeacher,
        IReadOnlyList<PersonLine> People,
        int OtherCount,
        long OtherSeconds)
    {
        public int Count(MeetParticipantMark mark) => People.Count(p => p.Mark == mark);
    }

    /// <summary>
    /// Spec §9: the lower-cased, distinct emails of the organizers and connections — the only emails the people lookup
    /// (db-design R5) is asked about.
    /// </summary>
    internal static IReadOnlyCollection<string> EmailsOf(IEnumerable<MeetReportMeetingRecord> meetings) =>
        meetings
            .SelectMany(m => m.Connections.Select(c => c.Email).Append(m.OrganizerEmail))
            .OfType<string>()
            .Select(Lower)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>The people lookup keyed by lower-cased email.</summary>
    internal static IReadOnlyDictionary<string, MeetReportPersonRecord> ByEmail(IEnumerable<MeetReportPersonRecord> people) =>
        people
            .GroupBy(p => Lower(p.LowerCaseEmail), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

    /// <summary>
    /// Spec FR-005 with the precedence of I-4 (organizer → course teacher → course student → not on the course list →
    /// other) and FR-006's capped seconds; other participants are counted by I-8 and their seconds summed per
    /// connection, each capped (openapi <c>MeetOtherParticipants.totalSeconds</c>).
    /// </summary>
    internal static MeetingFacts Classify(
        MeetReportMeetingRecord meeting,
        long courseId,
        IReadOnlyList<MeetReportMemberRecord> members,
        IReadOnlyDictionary<string, MeetReportPersonRecord> people,
        TimeZoneInfo zone)
    {
        var duration = Math.Max(0L, (long)(meeting.EndedAt - meeting.StartedAt).TotalSeconds);
        var date = RosterOnDate.LocalDate(meeting.StartedAt, zone);
        var teachers = OnRoster(members, courseId, ClassroomRole.Teacher, date, zone);
        var students = OnRoster(members, courseId, ClassroomRole.Student, date, zone);
        // US-038 spec I-9: an unknown organizer matches no connection and is not reported as "not a course teacher".
        var organizer = meeting.OrganizerEmail is null ? null : Lower(meeting.OrganizerEmail);

        var lines = new List<PersonLine>();
        var otherCount = 0;
        var otherSeconds = 0L;
        foreach (var connection in meeting.Connections.Where(c => c.Email is null))
        {
            otherCount++;
            otherSeconds += Math.Min(connection.DurationSeconds, duration);
        }

        foreach (var group in meeting.Connections
                     .Where(c => c.Email is not null)
                     .GroupBy(c => Lower(c.Email!), StringComparer.Ordinal))
        {
            MeetParticipantMark? mark =
                group.Key == organizer ? MeetParticipantMark.Organizer
                : teachers.Contains(group.Key) ? MeetParticipantMark.CourseTeacher
                : students.Contains(group.Key) ? MeetParticipantMark.CourseStudent
                : people.TryGetValue(group.Key, out var person) && person.HasAnyMembership ? MeetParticipantMark.NotOnCourseList
                : null;
            if (mark is { } identified)
            {
                var email = identified == MeetParticipantMark.Organizer ? meeting.OrganizerEmail! : group.First().Email!;
                lines.Add(new PersonLine(group.Key, email, identified, Math.Min(group.Sum(c => (long)c.DurationSeconds), duration)));
            }
            else
            {
                otherCount++;
                otherSeconds += group.Sum(c => Math.Min(c.DurationSeconds, duration));
            }
        }

        return new MeetingFacts(
            meeting,
            duration,
            organizer is not null && !teachers.Contains(organizer),
            lines,
            otherCount,
            otherSeconds);
    }

    /// <summary>Spec FR-011, OD-012: the participant's full name as Google gave it, with the email beside it.</summary>
    internal static PersonRef Person(string? email, IReadOnlyDictionary<string, MeetReportPersonRecord> people) =>
        email is null
            ? new PersonRef(null, null) // US-038 spec I-9: the organizer was erased or is excluded
            : new PersonRef(Name(people.TryGetValue(Lower(email), out var person) ? person.FullName : null), email);

    /// <summary>A name that is blank counts as no name (spec I-12).</summary>
    internal static string? Name(string? fullName) => string.IsNullOrWhiteSpace(fullName) ? null : fullName;

    /// <summary>
    /// Spec I-10: by displayed name (the full name, else the email) in the UI language's collation, unnamed last, then
    /// by email.
    /// </summary>
    internal static IOrderedEnumerable<T> OrderByPerson<T>(IEnumerable<T> items, Func<T, PersonRef> person, CultureInfo uiCulture)
    {
        var comparer = StringComparer.Create(uiCulture, ignoreCase: false);
        return items
            .OrderBy(i => Displayed(person(i)) is null)
            .ThenBy(i => Displayed(person(i)) ?? string.Empty, comparer)
            .ThenBy(i => person(i).Email ?? string.Empty, StringComparer.Ordinal);
    }

    /// <summary>Spec FR-007: a mean rounded half away from zero; zero when nothing is averaged.</summary>
    internal static long Mean(long total, int count) =>
        count == 0 ? 0 : (long)Math.Round((decimal)total / count, MidpointRounding.AwayFromZero);

    internal static string Lower(string email) => email.ToLowerInvariant();

    private static string? Displayed(PersonRef person) => person.FullName ?? person.Email;

    /// <summary>US-032 FR-002 reused: the lower-cased emails of the course's members in a role on the date.</summary>
    private static HashSet<string> OnRoster(
        IReadOnlyList<MeetReportMemberRecord> members,
        long courseId,
        ClassroomRole role,
        DateOnly date,
        TimeZoneInfo zone) =>
        members
            .Where(m => m.Role == role && m.Email is not null)
            .Where(m => RosterOnDate.Covers(
                new RosterMembership(courseId, m.Role, m.Email!, m.FirstSeenAt, m.LastSeenAt, m.OnRoster), date, zone))
            .Select(m => Lower(m.Email!))
            .ToHashSet(StringComparer.Ordinal);
}
