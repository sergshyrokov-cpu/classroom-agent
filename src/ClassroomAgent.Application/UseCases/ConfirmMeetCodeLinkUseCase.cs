using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using static ClassroomAgent.Application.UseCases.MeetCodeWrite;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Confirms an automatic, unconfirmed link (US-032 spec FR-009, FR-013).
/// Evaluation order: read-only guard (refused audit row, then <c>ReadOnlyModeException</c>) → shape → existence →
/// expected state → write and audit in one transaction.
/// </summary>
public sealed class ConfirmMeetCodeLinkUseCase(
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
            readOnlyMode, auditEvents, unitOfWork, writeScope, timeProvider, AuditAction.MeetCodeLinkConfirmed, actorId, actorRole, meetingCode, requestId, cancellationToken);

        var fields = Fields(form);
        var returnPage = fields is null ? 0 : ReturnPage(Value(fields, ReturnPageField));
        if (fields is null || !IsWellFormedCode(meetingCode) || Id(Value(fields, ExpectedCourseIdField)) is not { } expectedCourse)
        {
            return Result(MeetCodeChangeOutcome.Malformed, MeetCodeList.Linked, returnPage);
        }

        var code = meetingCode!;
        var link = await links.GetByCodeAsync(code, cancellationToken);
        if (link is null && !await links.CodeHasMeetingsAsync(code, cancellationToken))
        {
            return Result(MeetCodeChangeOutcome.CodeNotFound, MeetCodeList.Unassigned, returnPage);
        }

        // FR-013, I-9: the page offered "Confirm" only for an automatic, unconfirmed link on this course.
        if (link is null || !link.CanBeConfirmed || link.CourseId != expectedCourse)
        {
            return Result(MeetCodeChangeOutcome.StateChanged, ListOf(Current(link).State), 0);
        }

        var now = timeProvider.GetUtcNow();
        var committed = await CommitAsync(
            unitOfWork,
            () =>
            {
                link.Confirm(actorId, now);
                auditEvents.Add(AuditEvent.MeetCodeChanged(
                    AuditAction.MeetCodeLinkConfirmed, actorId, actorRole, code, expectedCourse, null, now, requestId));
            },
            cancellationToken);

        return committed
            ? Result(MeetCodeChangeOutcome.Confirmed, MeetCodeList.Linked, returnPage)
            : Result(MeetCodeChangeOutcome.StateChanged, MeetCodeList.Linked, 0);
    }
}
