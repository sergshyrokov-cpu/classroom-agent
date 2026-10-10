using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using static ClassroomAgent.Application.UseCases.MeetCodeWrite;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Sets a code's course: pick for an unassigned code, re-link, or remove the mark (US-032 spec FR-008, FR-010, FR-012, FR-013; api-design §2.3 … §2.5).
/// Evaluation order: read-only guard (refused audit row, then <c>ReadOnlyModeException</c>) → shape → existence →
/// expected state → write and audit in one transaction.
/// </summary>
public sealed class SetMeetCodeCourseUseCase(
    IReadOnlyModeGuard readOnlyMode,
    IMeetingCodeLinkRepository links,
    IAuditEventRepository auditEvents,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider)
{
    public async Task<MeetCodeChangeResult> ExecuteAsync(
        long actorId,
        AppRole actorRole,
        string? meetingCode,
        MeetCodeFormInput form,
        string? requestId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        var fields = Fields(form);

        // api-design §2.5: the refused row's action follows the expected state when it parses.
        var refusedAction = (fields is null ? null : State(Value(fields, ExpectedStateField))) switch
        {
            MeetCodeExpectedState.Linked => AuditAction.MeetCodeRelinked,
            MeetCodeExpectedState.Marked => AuditAction.MeetCodeMarkRemoved,
            _ => AuditAction.MeetCodeCoursePicked,
        };
        await EnsureAllowedAsync(
            readOnlyMode, auditEvents, unitOfWork, writeScope, timeProvider, refusedAction, actorId, actorRole, meetingCode, requestId, cancellationToken);

        var returnPage = fields is null ? 0 : ReturnPage(Value(fields, ReturnPageField));
        if (fields is null
            || !IsWellFormedCode(meetingCode)
            || Id(Value(fields, CourseIdField)) is not { } courseId
            || Expected(fields) is not { } expected)
        {
            return Result(MeetCodeChangeOutcome.Malformed, MeetCodeList.Unassigned, returnPage);
        }

        var code = meetingCode!;
        var link = await links.GetByCodeAsync(code, cancellationToken);
        if (link is null && !await links.CodeHasMeetingsAsync(code, cancellationToken))
        {
            return Result(MeetCodeChangeOutcome.CodeNotFound, MeetCodeList.Unassigned, returnPage);
        }

        if (!await links.CourseExistsAsync(courseId, cancellationToken))
        {
            return Result(MeetCodeChangeOutcome.CourseNotFound, ListOf(expected.State), returnPage);
        }

        var current = Current(link);
        if (current != expected)
        {
            return Result(MeetCodeChangeOutcome.StateChanged, ListOf(current.State), 0);
        }

        if (current.State == MeetCodeExpectedState.Linked && current.CourseId == courseId)
        {
            return Result(MeetCodeChangeOutcome.SameCourse, MeetCodeList.Linked, returnPage);
        }

        var now = timeProvider.GetUtcNow();
        var (outcome, action, previous) = current.State switch
        {
            MeetCodeExpectedState.Linked => (MeetCodeChangeOutcome.Relinked, AuditAction.MeetCodeRelinked, current.CourseId),
            MeetCodeExpectedState.Marked => (MeetCodeChangeOutcome.MarkRemoved, AuditAction.MeetCodeMarkRemoved, (long?)null),
            _ => (MeetCodeChangeOutcome.CoursePicked, AuditAction.MeetCodeCoursePicked, (long?)null),
        };

        var committed = await CommitAsync(
            unitOfWork,
            () =>
            {
                switch (current.State)
                {
                    case MeetCodeExpectedState.Linked:
                        link!.Relink(courseId, actorId, now);
                        break;
                    case MeetCodeExpectedState.Marked:
                        link!.LinkFromMark(courseId, actorId, now);
                        break;
                    default:
                        links.Add(MeetingCodeLink.LinkByPerson(code, courseId, actorId, now));
                        break;
                }

                auditEvents.Add(AuditEvent.MeetCodeChanged(action, actorId, actorRole, code, courseId, previous, now, requestId));
            },
            cancellationToken);

        return committed
            ? Result(outcome, ListOf(expected.State), returnPage)
            : Result(MeetCodeChangeOutcome.StateChanged, ListOf(expected.State), 0);
    }
}
