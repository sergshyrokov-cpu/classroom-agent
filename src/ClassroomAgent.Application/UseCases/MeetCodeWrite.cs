using System.Globalization;
using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// What the three meet-code writes share (US-032 spec FR-013 … FR-015, VR-001 … VR-003; api-design §2.5, §2.7, §2.8):
/// the read-only guard with its refused row, the shape of the code and of the posted fields, and the list a code's
/// state belongs to.
/// </summary>
internal static class MeetCodeWrite
{
    public const string Operation = "MeetCodes.Change";

    public const string CourseIdField = "courseId";

    public const string ExpectedStateField = "expectedState";

    public const string ExpectedCourseIdField = "expectedCourseId";

    public const string ReturnPageField = "returnPage";

    private static readonly string[] KnownFields = [CourseIdField, ExpectedStateField, ExpectedCourseIdField, ReturnPageField];

    /// <summary>
    /// The guard runs first (AD-6, api-design §2.5). Refused: one row of <paramref name="action"/> with the code when it
    /// is well-formed and no course, committed as the permitted audit write; then the refusal goes on to the host (409).
    /// </summary>
    public static async Task EnsureAllowedAsync(
        IReadOnlyModeGuard readOnlyMode,
        IAuditEventRepository auditEvents,
        IUnitOfWork unitOfWork,
        ServiceWriteScope writeScope,
        TimeProvider timeProvider,
        AuditAction action,
        long actorId,
        AppRole actorRole,
        string? meetingCode,
        string? requestId,
        CancellationToken cancellationToken)
    {
        try
        {
            await readOnlyMode.EnsureAllowedAsync(Operation, cancellationToken);
        }
        catch (ReadOnlyModeException)
        {
            auditEvents.Add(AuditEvent.MeetCodeChangeRefused(
                action,
                actorId,
                actorRole,
                IsWellFormedCode(meetingCode) ? meetingCode : null,
                timeProvider.GetUtcNow(),
                requestId));
            using (writeScope.Declare(PermittedServiceWrite.AuditEvent))
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            throw;
        }
    }

    /// <summary>VR-001: non-blank, at most 64 characters, no control characters; compared exactly as stored.</summary>
    public static bool IsWellFormedCode(string? meetingCode) =>
        !string.IsNullOrWhiteSpace(meetingCode)
        && meetingCode.Length <= MeetingCodeLink.MeetingCodeMaxLength
        && !meetingCode.Any(char.IsControl);

    /// <summary>
    /// The posted fields this form knows, each at most once (api-design §2.8). Null when a known field is repeated;
    /// unknown names are ignored.
    /// </summary>
    public static IReadOnlyDictionary<string, string?>? Fields(MeetCodeFormInput form)
    {
        var fields = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (name, value) in form.Fields)
        {
            if (!KnownFields.Contains(name, StringComparer.Ordinal))
            {
                continue;
            }

            if (!fields.TryAdd(name, value))
            {
                return null;
            }
        }

        return fields;
    }

    /// <summary>VR-003: <c>unassigned</c>, <c>linked</c> or <c>marked</c>, case-sensitive.</summary>
    public static MeetCodeExpectedState? State(string? value) => value switch
    {
        "unassigned" => MeetCodeExpectedState.Unassigned,
        "linked" => MeetCodeExpectedState.Linked,
        "marked" => MeetCodeExpectedState.Marked,
        _ => null,
    };

    /// <summary>VR-002: decimal digits only, a positive 64-bit integer.</summary>
    public static long? Id(string? value) =>
        value is { Length: > 0 } && value.All(char.IsAsciiDigit)
        && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
            ? id
            : null;

    /// <summary>api-design §2.7: a navigation aid — a whole number 0 … int.MaxValue, anything else 0.</summary>
    public static int ReturnPage(string? value) =>
        value is { Length: > 0 } && value.All(char.IsAsciiDigit)
        && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var page)
            ? page
            : 0;

    public static string? Value(IReadOnlyDictionary<string, string?> fields, string name) =>
        fields.TryGetValue(name, out var value) ? value : null;

    /// <summary>
    /// The expected state with its course, or null when malformed: <c>expectedCourseId</c> is required exactly when the
    /// state is <c>linked</c> (VR-003).
    /// </summary>
    public static (MeetCodeExpectedState State, long? CourseId)? Expected(IReadOnlyDictionary<string, string?> fields)
    {
        if (State(Value(fields, ExpectedStateField)) is not { } state)
        {
            return null;
        }

        var hasCourse = fields.ContainsKey(ExpectedCourseIdField);
        if (state == MeetCodeExpectedState.Linked)
        {
            return Id(Value(fields, ExpectedCourseIdField)) is { } course ? (state, course) : null;
        }

        return hasCourse ? null : (state, null);
    }

    /// <summary>The state a code is in now: no row, a link on a course, or a mark.</summary>
    public static (MeetCodeExpectedState State, long? CourseId) Current(MeetingCodeLink? link) =>
        link is null
            ? (MeetCodeExpectedState.Unassigned, null)
            : link.State == MeetingCodeLinkState.Linked
                ? (MeetCodeExpectedState.Linked, link.CourseId)
                : (MeetCodeExpectedState.Marked, null);

    public static MeetCodeList ListOf(MeetCodeExpectedState state) => state switch
    {
        MeetCodeExpectedState.Linked => MeetCodeList.Linked,
        MeetCodeExpectedState.Marked => MeetCodeList.NotACourse,
        _ => MeetCodeList.Unassigned,
    };

    public static MeetCodeChangeResult Result(MeetCodeChangeOutcome outcome, MeetCodeList list, int returnPage) =>
        new(outcome, list, returnPage);

    /// <summary>
    /// Stages the change and its audit row and commits them in one transaction (AD-7); a concurrent change found at the
    /// commit is the stale state (spec FR-013, db-design §7). True when committed.
    /// </summary>
    public static async Task<bool> CommitAsync(
        IUnitOfWork unitOfWork,
        Action stage,
        CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    stage();
                    await unitOfWork.SaveChangesAsync(ct);
                },
                cancellationToken);
            return true;
        }
        catch (MeetingCodeLinkConflictException)
        {
            return false;
        }
    }
}
