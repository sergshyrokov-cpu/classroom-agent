using System.Globalization;
using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-062 entity model §2.4, spec FR-006.2: the Admin confirms that courses not visible to the technical account were
/// deleted in Classroom — read-only guard, VR-001, then one transaction {lock, VR-002, marks, audit}.
/// </summary>
/// <remarks>
/// All-or-nothing (spec I-5): if any selected course is unknown or no longer "not visible", nothing is staged and no
/// audit row is written (AC-017). The rows are locked <c>FOR UPDATE</c> for the transaction, so a concurrent
/// synchronization or confirmation of the same course waits and then sees the outcome (db-design §4).
/// </remarks>
public sealed class ConfirmCoursesDeletedUseCase(
    IReadOnlyModeGuard readOnlyMode,
    ICourseRepository courses,
    IAuditEventRepository auditEvents,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider)
{
    /// <summary>The operation name the read-only guard records; a constant, never user data (SC-10).</summary>
    public const string Operation = "DataHealth.ConfirmCoursesDeleted";

    /// <summary>VR-001: a positive 64-bit integer, written plainly — no sign, no leading zero, no space.</summary>
    private const int MaxIdDigits = 19;

    public async Task<CourseDeletionConfirmationResult> ExecuteAsync(
        long adminId, CourseDeletionConfirmationForm form, string? requestId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);

        // 1: read-only mode, before the shape is looked at (AD-6, BR-026, api-design §2.4).
        try
        {
            await readOnlyMode.EnsureAllowedAsync(Operation, cancellationToken);
        }
        catch (ReadOnlyModeException)
        {
            await AuditRefusalAsync(adminId, requestId, cancellationToken);
            throw;
        }

        // 2: VR-001, I-6.
        if (Parse(form.CourseIds) is not { } ids)
        {
            return new CourseDeletionConfirmationResult(CourseDeletionConfirmationOutcome.Invalid, 0);
        }

        // 3 … 5: one transaction; the lock makes the state check and the marks one step (db-design §4).
        var confirmed = 0;
        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var found = await courses.GetForAbsenceConfirmationAsync(ids, token);
                if (found.Count != ids.Count
                    || found.Any(c => c.AbsenceMark != CourseAbsenceMark.NotVisibleToTechnicalAccount))
                {
                    return;
                }

                var now = timeProvider.GetUtcNow();
                foreach (var course in found)
                {
                    course.ConfirmDeletedInClassroom(now);
                }

                auditEvents.Add(AuditEvent.CoursesConfirmedDeleted(adminId, ids, now, requestId));
                await unitOfWork.SaveChangesAsync(token);
                confirmed = found.Count;
            },
            cancellationToken);

        return confirmed == 0
            ? new CourseDeletionConfirmationResult(CourseDeletionConfirmationOutcome.ListChanged, 0)
            : new CourseDeletionConfirmationResult(CourseDeletionConfirmationOutcome.Confirmed, confirmed);
    }

    /// <summary>VR-001: at least one id, at most 1000 after removing duplicates, each a positive 64-bit integer.</summary>
    private static IReadOnlyList<long>? Parse(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        var ids = new HashSet<long>();
        foreach (var value in values)
        {
            if (value is null
                || value.Length is 0 or > MaxIdDigits
                || value[0] == '0'
                || !value.All(char.IsAsciiDigit)
                || !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                return null;
            }

            ids.Add(id);
        }

        return ids.Count > AuditEvent.MaxConfirmedCourseIds ? null : ids.Order().ToList();
    }

    /// <summary>
    /// Api-design §2.5: one refused audit row without course ids (unverified input), declared as
    /// <see cref="PermittedServiceWrite.AuditEvent"/> — on the BR-026 closed list — so it is written in read-only mode.
    /// </summary>
    private async Task AuditRefusalAsync(long adminId, string? requestId, CancellationToken cancellationToken)
    {
        auditEvents.Add(AuditEvent.CoursesConfirmedDeletedRefused(
            adminId, AuditRefusalCategory.ReadOnlyMode, timeProvider.GetUtcNow(), requestId));
        using (writeScope.Declare(PermittedServiceWrite.AuditEvent))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
