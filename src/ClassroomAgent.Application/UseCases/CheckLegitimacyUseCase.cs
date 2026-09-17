using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// One legitimacy check (US-005 spec FR-007; db-design §4.2): asks the Control Plane, records the answer in
/// <see cref="LegitimacyState"/> and reports the outcome. A successful check moves the last success; an
/// <c>upgrade_required</c> answer records everything else; any other failure writes nothing. Writing
/// <see cref="LegitimacyState"/> is on the BR-026 closed list, so no read-only guard applies. Failures are
/// outcomes, not exceptions (AD-9); only the caller's cancellation propagates.
/// </summary>
public sealed class CheckLegitimacyUseCase(
    IControlPlaneClient controlPlane,
    ILegitimacyStateRepository states,
    IUnitOfWork unitOfWork,
    InstallationIdentity identity,
    TimeProvider timeProvider)
{
    public async Task<CheckOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        ControlPlaneCheckReply reply;
        try
        {
            reply = await controlPlane.CheckAsync(
                identity.InstallationId,
                identity.ApplicationVersion,
                identity.ContractVersion,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Whatever the client throws is an unsuccessful check; its text never leaves here (SC-10).
            return CheckOutcome.Failed(LegitimacyCheckFailure.UnexpectedError);
        }

        if (reply is ControlPlaneCheckReply.Failure failure)
        {
            return CheckOutcome.Failed(FailureOf(failure.Category));
        }

        var answer = (ControlPlaneCheckReply.Answer)reply;
        return await RecordAsync(answer, cancellationToken);
    }

    private async Task<CheckOutcome> RecordAsync(ControlPlaneCheckReply.Answer answer, CancellationToken cancellationToken)
    {
        var upgradeRequired = answer.Compatibility == CompatibilityState.UpgradeRequired;
        try
        {
            var state = await states.GetAsync(cancellationToken);
            if (upgradeRequired)
            {
                if (state is null)
                {
                    states.Add(LegitimacyState.FromUpgradeRequired(answer.Status, answer.Domain, answer.ClientId));
                }
                else
                {
                    state.RecordUpgradeRequired(answer.Status, answer.Domain, answer.ClientId);
                }
            }
            else
            {
                // The completion time of this check (FR-007).
                var checkedAt = timeProvider.GetUtcNow();
                if (state is null)
                {
                    states.Add(LegitimacyState.FromSuccess(checkedAt, answer.Status, answer.Compatibility, answer.Domain, answer.ClientId));
                }
                else
                {
                    state.RecordSuccess(checkedAt, answer.Status, answer.Compatibility, answer.Domain, answer.ClientId);
                }
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // The database is unavailable or refused the row: the stored state stays as it was (FR-007).
            return CheckOutcome.Failed(LegitimacyCheckFailure.SaveFailed, answer.Status, answer.Compatibility);
        }

        return upgradeRequired
            ? CheckOutcome.Failed(LegitimacyCheckFailure.UpgradeRequired, answer.Status, answer.Compatibility)
            : CheckOutcome.Success(answer.Status, answer.Compatibility);
    }

    private static LegitimacyCheckFailure FailureOf(CheckFailureCategory category) => category switch
    {
        CheckFailureCategory.Unreachable => LegitimacyCheckFailure.Unreachable,
        CheckFailureCategory.Timeout => LegitimacyCheckFailure.Timeout,
        CheckFailureCategory.ErrorAnswer => LegitimacyCheckFailure.ErrorAnswer,
        CheckFailureCategory.UnparseableAnswer => LegitimacyCheckFailure.UnparseableAnswer,
        CheckFailureCategory.UnknownInstallation => LegitimacyCheckFailure.UnknownInstallation,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };
}
