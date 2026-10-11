using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using static ClassroomAgent.Application.UseCases.JournalQueryRules;
using static ClassroomAgent.Application.UseCases.MeetReportRules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Report 4 — one meeting of the course in detail (US-033 spec FR-010, VR-003, api-design §2.4). Reads only, like the
/// reports page (spec FR-015).
/// </summary>
/// <remarks>
/// <c>courseId</c> is required and validated before anything is read. An unusable meeting id, an unknown course and a
/// meeting not of the course all answer "not found", so the page does not reveal a meeting of another course. The
/// period is not validated: a valid <c>from</c> or <c>to</c> only rebuilds the link back (spec I-1).
/// </remarks>
public sealed class GetMeetReportMeetingQuery(IMeetReportSource source, SchoolTimeZone schoolTimeZone)
{
    public async Task<MeetReportMeetingPageResult> ExecuteAsync(
        MeetReportMeetingRequest request,
        CultureInfo uiCulture,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(uiCulture);

        long? course = Single(request.CourseId, out var courseText) == Presence.One ? ParseCourseId(courseText) : null;
        var backLink = new MeetReportBackLink(course, BackDate(request.From), BackDate(request.To));

        MeetReportMeetingPageResult Page(MeetReportMeetingOutcome outcome, MeetReportMeetingMessageKey? key, MeetingDetail? detail = null) =>
            new(outcome, new MeetReportMeetingPageModel(backLink, key, detail));

        if (course is not { } courseId)
        {
            return Page(MeetReportMeetingOutcome.Invalid, MeetReportMeetingMessageKey.CourseMalformed);
        }

        if (ParseCourseId(request.MeetingId ?? string.Empty) is not { } meetingId)
        {
            return Page(MeetReportMeetingOutcome.NotFound, MeetReportMeetingMessageKey.MeetingNotFound);
        }

        var courseName = await source.GetCourseNameAsync(courseId, cancellationToken);
        var meeting = courseName is null ? null : await source.GetMeetingOfCourseAsync(courseId, meetingId, cancellationToken);
        if (courseName is null || meeting is null)
        {
            return Page(MeetReportMeetingOutcome.NotFound, MeetReportMeetingMessageKey.MeetingNotFound);
        }

        var zone = schoolTimeZone.Zone;
        var members = await source.GetCourseMembersAsync(courseId, cancellationToken);
        var people = ByEmail(await source.GetPeopleAsync(EmailsOf([meeting]), cancellationToken));
        var facts = Classify(meeting, courseId, members, people, zone);

        // Spec FR-010: organizer first, then course teachers, course students and not on the course list, each by name.
        var lines = OrderByPerson(
                facts.People.Select(p => new MeetingPersonLine(Person(p.Email, people), p.Mark, p.Seconds)),
                l => l.Person,
                uiCulture)
            .ToList()
            .OrderBy(l => l.Mark)
            .ToList();

        return Page(
            MeetReportMeetingOutcome.Shown,
            null,
            new MeetingDetail(
                courseName,
                meeting.MeetingCode,
                TimeZoneInfo.ConvertTime(meeting.StartedAt, zone).DateTime,
                TimeZoneInfo.ConvertTime(meeting.EndedAt, zone).DateTime,
                facts.DurationSeconds,
                Person(meeting.OrganizerEmail, people),
                facts.OrganizerNotCourseTeacher,
                lines,
                new MeetOtherParticipants(facts.OtherCount, facts.OtherSeconds)));
    }

    /// <summary>api-design §2.4: a date of the link back — kept only when it is one valid date, otherwise dropped.</summary>
    private static DateOnly? BackDate(IReadOnlyList<string?> values) =>
        Single(values, out _) == Presence.One ? ReadDate(values, default, out _) : null;
}
