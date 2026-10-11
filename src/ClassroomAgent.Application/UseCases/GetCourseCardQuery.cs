using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The course card (US-021 spec FR-001 … FR-016). Reads only: no read-only guard, no unit of work, no audit, no Google
/// port (spec FR-014).
/// </summary>
/// <remarks>
/// Order (api-design §3): the parse of the path value → the course (Q1) → its memberships (Q2). Nothing is read before
/// the parse passes, and Q2 is not run for an unknown course (spec FR-011). The port returns the members in card order
/// (db-design §2 Q2); this class splits them into the three sections keeping that order (spec FR-003, FR-007).
/// </remarks>
public sealed class GetCourseCardQuery(ICourseCardSource source, SchoolTimeZone schoolTimeZone)
{
    public async Task<CourseCardResult> ExecuteAsync(CourseCardRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Parse(request.CourseId) is not { } courseId)
        {
            return new CourseCardResult(CourseCardOutcome.CourseMalformed, null);
        }

        var course = await source.GetCourseAsync(courseId, cancellationToken);
        if (course is null)
        {
            return new CourseCardResult(CourseCardOutcome.CourseUnknown, null);
        }

        var members = await source.GetMembersAsync(courseId, cancellationToken);
        var teachers = new List<CourseCardTeacher>();
        var students = new List<CourseCardPerson>();
        var leavers = new List<CourseCardLeaver>();
        foreach (var member in members)
        {
            var name = CoursePersonNameRule.NameOf(member.Surname, member.GivenName, member.Email);
            var email = string.IsNullOrEmpty(member.Email) ? null : member.Email;
            if (!member.OnRoster)
            {
                leavers.Add(new CourseCardLeaver(member.ParticipantId, name, email, member.Role, LocalDate(member.LastSeenAt)));
            }
            else if (member.Role == ClassroomRole.Teacher)
            {
                // Spec FR-004, I-7: the owner mark is kept on a current teacher only.
                teachers.Add(new CourseCardTeacher(member.ParticipantId, name, email, member.IsOwner));
            }
            else
            {
                students.Add(new CourseCardPerson(member.ParticipantId, name, email));
            }
        }

        var details = new CourseCardDetails(
            course.CourseId,
            course.Name,
            Given(course.Section),
            course.State,
            Given(course.Room),
            Given(course.DescriptionHeading),
            Given(course.Description),
            LocalTime(course.CreationTime),
            LocalTime(course.UpdateTime));
        // Spec FR-010, api-design §2.1: the card sits under the course list; the parsed id in invariant decimal form.
        var returnPath = CourseListUrl.Path + "/" + courseId.ToString(CultureInfo.InvariantCulture);
        return new CourseCardResult(
            CourseCardOutcome.Shown,
            new CourseCardPageModel(details, teachers, students, leavers, returnPath));
    }

    /// <summary>Spec VR-001: a positive 64-bit integer of ASCII digits only; anything else is malformed.</summary>
    private static long? Parse(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.All(char.IsAsciiDigit)
        && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
        && id > 0
            ? id
            : null;

    /// <summary>Spec I-1: null, empty or white space only is not given; a given value is passed unchanged.</summary>
    private static string? Given(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Spec FR-002: the school's local time, to the minute; null stays null.</summary>
    private DateTime? LocalTime(DateTimeOffset? instant)
    {
        if (instant is not { } value)
        {
            return null;
        }

        var local = TimeZoneInfo.ConvertTime(value, schoolTimeZone.Zone).DateTime;
        return new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0, DateTimeKind.Unspecified);
    }

    /// <summary>Spec FR-006, I-5: the school's local date of the instant.</summary>
    private DateOnly LocalDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, schoolTimeZone.Zone).DateTime);
}
