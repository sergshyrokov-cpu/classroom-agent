using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.MeetLinking;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The linking step of a synchronization run, right after the Meet step (US-032 spec FR-006): every unassigned code is
/// scored and decided; each unambiguous one gets an automatic link and its audit row in one transaction.
/// </summary>
/// <remarks>
/// Codes are read in batches in code order (db-design §5.2), so a run never holds the whole Meet history. Only codes
/// with no link row are read: a link — automatic or a person's — and a mark are never scored or changed (BR-065,
/// BR-083). A code a person decided while the batch was being scored fails the insert on the unique code; it is
/// skipped, not a run failure (db-design §7). It calls no Google port.
/// <para>
/// The step is a part of <see cref="RunSynchronizationUseCase"/>, whose read-only guard covers it (spec FR-006: no
/// linking outside a run, no second guard call). It therefore does not take <see cref="IUnitOfWork"/> in its
/// constructor — that would make it a write path of its own (US-007 AC-007) — but receives the run's unit of work as
/// an argument from the guarded run.
/// </para>
/// </remarks>
public sealed class LinkMeetCodesStep(
    IMeetCodeScoringSource scoring,
    IMeetingCodeLinkRepository links,
    IAuditEventRepository auditEvents,
    TimeProvider timeProvider,
    SchoolTimeZone zone,
    MeetLinkingThresholds thresholds)
{
    /// <summary>db-design §5.2: how many codes one batch scores.</summary>
    public const int BatchSize = 200;

    public async Task<MeetLinkingCounts> ExecuteAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        var scored = 0;
        var created = 0;
        string? after = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var codes = await scoring.GetUnassignedCodesAsync(after, BatchSize, cancellationToken);
            if (codes.Count == 0)
            {
                break;
            }

            after = codes[^1];
            foreach (var input in await scoring.GetScoringInputsAsync(codes, cancellationToken))
            {
                scored++;
                var decision = MeetCodeScorer.Decide(MeetCodeScorer.Score(input, zone.Zone), thresholds);
                if (decision is { Unambiguous: true, CourseId: { } courseId }
                    && await LinkAsync(unitOfWork, input.MeetingCode, courseId, cancellationToken))
                {
                    created++;
                }
            }

            if (codes.Count < BatchSize)
            {
                break;
            }
        }

        return new MeetLinkingCounts(scored, created);
    }

    private async Task<bool> LinkAsync(
        IUnitOfWork unitOfWork,
        string meetingCode,
        long courseId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    links.Add(MeetingCodeLink.LinkAutomatically(meetingCode, courseId, now));
                    auditEvents.Add(AuditEvent.MeetCodeAutoLinked(meetingCode, courseId, now));
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
