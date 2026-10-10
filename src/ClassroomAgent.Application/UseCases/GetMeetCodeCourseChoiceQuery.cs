using System.Globalization;
using ClassroomAgent.Application.MeetLinking;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The course-choice form for one code (US-032 spec FR-008, FR-010, FR-012): candidates first, best share first, then
/// every other stored course by name; the course the code is linked to is not offered. Reads only.
/// </summary>
public sealed class GetMeetCodeCourseChoiceQuery(
    IMeetingCodeLinkRepository links,
    IMeetCodesReadSource reads,
    IMeetCodeScoringSource scoring,
    SchoolTimeZone zone)
{
    /// <summary>
    /// <paramref name="returnPage"/> is the raw value (malformed → 0). Result: <c>Malformed</c> for a code failing its shape,
    /// a null model for an unknown code.
    /// </summary>
    public async Task<MeetCodeChoiceResult> ExecuteAsync(
        string? meetingCode,
        string? returnPage,
        MeetCodeFieldError? fieldError,
        CancellationToken cancellationToken)
    {
        if (!MeetCodeWrite.IsWellFormedCode(meetingCode))
        {
            return new MeetCodeChoiceResult(true, null);
        }

        var code = meetingCode!;
        var link = await links.GetByCodeAsync(code, cancellationToken);
        if (link is null && !await links.CodeHasMeetingsAsync(code, cancellationToken))
        {
            return new MeetCodeChoiceResult(false, null);
        }

        var (state, currentCourseId) = MeetCodeWrite.Current(link);
        var courses = (await reads.GetAllCoursesAsync(cancellationToken)).ToDictionary(c => c.Id);
        var inputs = await scoring.GetScoringInputsAsync([code], cancellationToken);
        var candidates = inputs.Count == 0
            ? []
            : GetMeetCodesQuery.Candidates(MeetCodeScorer.Score(inputs[0], zone.Zone), courses)
                .Where(c => c.Course.Id != currentCourseId)
                .ToList();

        var offered = candidates.Select(c => c.Course.Id).ToHashSet();
        var others = courses.Values
            .Where(c => c.Id != currentCourseId && !offered.Contains(c.Id))
            .OrderBy(c => c.Name, StringComparer.Create(CultureInfo.CurrentUICulture, ignoreCase: false))
            .ThenBy(c => c.Id)
            .ToList();

        var current = currentCourseId is { } id && courses.TryGetValue(id, out var course) ? course : null;
        return new MeetCodeChoiceResult(
            false,
            new MeetCodeCourseChoicePageModel(code, state, current, candidates, others, MeetCodeWrite.ReturnPage(returnPage), fieldError));
    }
}
