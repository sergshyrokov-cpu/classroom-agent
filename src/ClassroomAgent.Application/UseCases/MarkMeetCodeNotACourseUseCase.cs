using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using static ClassroomAgent.Application.UseCases.MeetCodeWrite;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Marks a code "not a course" (US-032 spec FR-011, FR-013; BR-083).
/// Evaluation order: read-only guard (refused audit row, then <c>ReadOnlyModeException</c>) → shape → existence →
/// expected state → write and audit in one transaction.
/// </summary>
public sealed class MarkMeetCodeNotACourseUseCase(
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
        await EnsureAllowedAsync(
            readOnlyMode, auditEvents, unitOfWork, writeScope, timeProvider, AuditAction.MeetCodeMarkedNotACourse, actorId, actorRole, meetingCode, requestId, cancellationToken);

        var fields = Fields(form);
        var returnPage = fields is null ? 0 : ReturnPage(Value(fields, ReturnPageField));

        // api-design: a marked code is already marked, so "marked" is never a valid expected state here.
        if (fields is null
            || !IsWellFormedCode(meetingCode)
            || Expected(fields) is not { } expected
            || expected.State == MeetCodeExpectedState.Marked)
        {
            return Result(MeetCodeChangeOutcome.Malformed, MeetCodeList.Unassigned, returnPage);
        }

        var code = meetingCode!;
        var link = await links.GetByCodeAsync(code, cancellationToken);
        if (link is null && !await links.CodeHasMeetingsAsync(code, cancellationToken))
        {
            return Result(MeetCodeChangeOutcome.CodeNotFound, MeetCodeList.Unassigned, returnPage);
        }

        var current = Current(link);
        if (current != expected)
        {
            return Result(MeetCodeChangeOutcome.StateChanged, ListOf(current.State), 0);
        }

        var now = timeProvider.GetUtcNow();
        var committed = await CommitAsync(
            unitOfWork,
            () =>
            {
                if (link is null)
                {
                    links.Add(MeetingCodeLink.MarkNotACourse(code, actorId, now));
                }
                else
                {
                    link.Mark(actorId, now);
                }

                auditEvents.Add(AuditEvent.MeetCodeChanged(
                    AuditAction.MeetCodeMarkedNotACourse, actorId, actorRole, code, null, current.CourseId, now, requestId));
            },
            cancellationToken);

        return committed
            ? Result(MeetCodeChangeOutcome.Marked, ListOf(expected.State), returnPage)
            : Result(MeetCodeChangeOutcome.StateChanged, ListOf(expected.State), 0);
    }
}
