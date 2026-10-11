using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using static ClassroomAgent.Application.UseCases.JournalCellRule;
using static ClassroomAgent.Application.UseCases.JournalQueryRules;
using static ClassroomAgent.Application.UseCases.MeetReportRules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Reports 1–3 of one course for a period (US-033 spec FR-001 … FR-016). Reads only: no read-only guard, no unit of
/// work, no audit, no Google port (spec FR-015).
/// </summary>
/// <remarks>
/// The query is validated with the journal's rules before any report data is read (spec FR-013, VR-001, VR-002). The
/// course list is the journal's (spec FR-002) and decides whether the course exists. The reads are a fixed number per
/// request (db-design §3): meetings with their connections, the course's members, the people behind the emails found
/// and the Meet watermark.
/// </remarks>
public sealed class GetMeetReportsQuery(
    IJournalSource courses,
    IMeetReportSource source,
    GetLastSynchronizationQuery lastSynchronization,
    SchoolTimeZone schoolTimeZone,
    TimeProvider timeProvider)
{
    public async Task<MeetReportsPageResult> ExecuteAsync(
        MeetReportsRequest request,
        CultureInfo uiCulture,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(uiCulture);

        // Spec FR-003: the current month is the one containing "now" in the school's zone.
        var zone = schoolTimeZone.Zone;
        var today = DateIn(timeProvider.GetUtcNow(), zone);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var messages = new List<MeetReportsMessageKey>();
        long? course = null;
        switch (Single(request.CourseId, out var courseText))
        {
            case Presence.Absent:
                break;
            case Presence.One when ParseCourseId(courseText) is { } id:
                course = id;
                break;
            default:
                messages.Add(MeetReportsMessageKey.CourseMalformed);
                break;
        }

        var from = ReadDate(request.From, monthStart, out var fromMalformed);
        if (fromMalformed)
        {
            messages.Add(MeetReportsMessageKey.FromMalformed);
        }

        var to = ReadDate(request.To, monthStart.AddMonths(1).AddDays(-1), out var toMalformed);
        if (toMalformed)
        {
            messages.Add(MeetReportsMessageKey.ToMalformed);
        }

        if (from is { } f && to is { } t && f > t)
        {
            messages.Add(MeetReportsMessageKey.PeriodInverted);
        }

        // Spec FR-002: the journal's drop-down, which also decides whether the course exists.
        var records = await courses.GetCoursesAsync(cancellationToken);
        var comparer = StringComparer.Create(uiCulture, ignoreCase: false);
        var options = records
            .OrderBy(c => c.Name, comparer)
            .ThenBy(c => c.Id)
            .Select(c => new CourseOption(c.Id, c.Name, c.Section))
            .ToList();
        var known = course is { } chosen && records.Any(c => c.Id == chosen) ? course : null;

        MeetReportsPageResult Page(MeetReportsPageOutcome outcome, MeetReports? reports = null) =>
            new(outcome, new MeetReportsPageModel(options, records.Count == 0, known, from, to, messages, reports));

        if (messages.Count > 0)
        {
            return Page(MeetReportsPageOutcome.Invalid);
        }

        if (course is null)
        {
            return Page(MeetReportsPageOutcome.Shown);
        }

        if (known is not { } courseId)
        {
            messages.Add(MeetReportsMessageKey.CourseUnknown);
            return Page(MeetReportsPageOutcome.CourseUnknown);
        }

        // Spec FR-003, FR-004: whole local days as one half-open UTC interval on the meeting's start.
        var start = StartOfDay(from!.Value, zone);
        var end = StartOfDay(to!.Value.AddDays(1), zone);

        var meetings = await source.GetMeetingsAsync(courseId, start, end, cancellationToken);
        var members = await source.GetCourseMembersAsync(courseId, cancellationToken);
        var emails = EmailsOf(meetings);
        var people = ByEmail(emails.Count == 0 ? [] : await source.GetPeopleAsync(emails, cancellationToken));
        var watermark = (await lastSynchronization.ExecuteAsync(cancellationToken)).MeetLoadedUpTo;

        // Spec FR-008: by start, then by internal id.
        var facts = meetings
            .OrderBy(m => m.StartedAt)
            .ThenBy(m => m.Id)
            .Select(m => Classify(m, courseId, members, people, zone))
            .ToList();

        var rows = facts
            .Select(m => new MeetListRow(
                m.Meeting.Id,
                TimeZoneInfo.ConvertTime(m.Meeting.StartedAt, zone).DateTime,
                TimeZoneInfo.ConvertTime(m.Meeting.EndedAt, zone).DateTime,
                m.DurationSeconds,
                Person(m.Meeting.OrganizerEmail, people),
                m.OrganizerNotCourseTeacher,
                m.Count(MeetParticipantMark.CourseTeacher),
                m.Count(MeetParticipantMark.CourseStudent),
                m.Count(MeetParticipantMark.NotOnCourseList),
                m.OtherCount))
            .ToList();

        // Spec FR-007: derived from report 2's rows.
        var total = rows.Sum(r => r.DurationSeconds);
        var summary = new MeetSummary(
            rows.Count,
            total,
            Mean(total, rows.Count),
            rows.Count == 0
                ? 0m
                : Math.Round((decimal)rows.Sum(r => r.CourseStudents) / rows.Count, 1, MidpointRounding.AwayFromZero));

        // Spec FR-009: the students of the period; a meeting counts for one only where they were a course student.
        var students = members
            .Where(m => m.Role == ClassroomRole.Student && IsOfPeriod(m.FirstSeenAt, m.LastSeenAt, m.OnRoster, start, end))
            .Select(m =>
            {
                var attended = m.Email is null
                    ? []
                    : facts
                        .Select(f => f.People.FirstOrDefault(p =>
                            p.Mark == MeetParticipantMark.CourseStudent && p.LowerCaseEmail == Lower(m.Email)))
                        .OfType<PersonLine>()
                        .ToList();
                return (Member: m, Row: new StudentParticipationRow(
                    new PersonRef(Name(m.FullName), m.Email),
                    attended.Count,
                    attended.Sum(p => p.Seconds)));
            });
        var studentRows = OrderByPerson(students, s => s.Row.Student, uiCulture)
            .ThenBy(s => s.Member.ParticipantId)
            .Select(s => s.Row)
            .ToList();

        return Page(
            MeetReportsPageOutcome.Shown,
            new MeetReports(new MeetDelayNotice(watermark), summary, rows, studentRows));
    }
}
