using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-038 spec FR-003: what the erasure of one participant will touch, the no-email notice (OD-001 a) and the
/// read-only notice (I-8; display only — the refusal is the use case's, AD-6). Null when no participant has that id.
/// Writes nothing and calls no Google port.
/// </summary>
public sealed class GetErasureConfirmationQuery(IParticipantErasureReadSource source, GetLegitimacyModeQuery mode)
{
    public async Task<ErasureConfirmationPageModel?> ExecuteAsync(long participantId, CancellationToken cancellationToken)
    {
        var data = await source.GetConfirmationAsync(participantId, cancellationToken);
        if (data is null)
        {
            return null;
        }

        var legitimacy = await mode.ExecuteAsync(cancellationToken);
        return new ErasureConfirmationPageModel(
            data.ParticipantId,
            data.FullName,
            data.Email,
            data.Courses
                .Select(c => new ErasureCourseEntry(
                    c.CourseName,
                    c.Role == ClassroomRole.Teacher ? ErasureCourseRole.Teacher : ErasureCourseRole.Student))
                .ToList(),
            data.Submissions,
            data.Grades,
            data.MeetParticipations,
            data.OrganizedMeetings,
            data.OwnedCourses,
            NoEmailNotice: string.IsNullOrWhiteSpace(data.Email),
            ReadOnlyNotice: legitimacy.IsReadOnly,
            MarkError: null);
    }
}
