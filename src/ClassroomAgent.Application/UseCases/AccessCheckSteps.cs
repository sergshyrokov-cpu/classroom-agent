using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The eight steps of one access check (US-011 spec FR-001…FR-003, I-2, I-5): six delegated-token requests, one per
/// scope of <see cref="GoogleDelegationScopes.All"/>, then the Classroom read and the Admin Reports read. One
/// implementation serves "Проверить доступ" and the startup self-check (spec FR-010), so the two cannot drift.
/// </summary>
/// <remarks>
/// Static and taking the port as an argument, because it is not a use case: it never decides whether Google may be
/// called. Both callers take <see cref="IReadOnlyModeGuard"/> and call it before they get here (US-007 FR-007).
/// </remarks>
public static class AccessCheckSteps
{
    /// <summary>Spec I-2: one check never takes longer than this, whatever Google does.</summary>
    public static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(30);

    public static async Task<AccessCheckResult> RunAsync(
        IGoogleAccessProbe probe,
        string technicalAccount,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentException.ThrowIfNullOrWhiteSpace(technicalAccount);
        ArgumentNullException.ThrowIfNull(timeProvider);

        using var limit = new CancellationTokenSource(TimeLimit, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limit.Token);
        var steps = new List<AccessCheckStep>(8);
        var tokens = new Dictionary<string, DelegatedToken>(StringComparer.Ordinal);
        var delegation = new Dictionary<string, AccessCheckStepOutcome>(StringComparer.Ordinal);
        AccessCheckStepOutcome? runWide = null;
        var timedOut = false;

        foreach (var scope in GoogleDelegationScopes.All)
        {
            if (timedOut)
            {
                steps.Add(new AccessCheckStep(AccessCheckStepKind.Delegation, scope, AccessCheckStepOutcome.GoogleUnavailable, null));
                delegation[scope] = AccessCheckStepOutcome.GoogleUnavailable;
                continue;
            }

            if (runWide is { } cause)
            {
                steps.Add(NotAttempted(AccessCheckStepKind.Delegation, scope, cause));
                delegation[scope] = AccessCheckStepOutcome.NotAttempted;
                continue;
            }

            try
            {
                var attempt = await probe.RequestDelegatedTokenAsync(technicalAccount, scope, linked.Token);
                steps.Add(new AccessCheckStep(AccessCheckStepKind.Delegation, scope, attempt.Outcome, null));
                delegation[scope] = attempt.Outcome;
                if (attempt is { Outcome: AccessCheckStepOutcome.Succeeded, Token: { } token })
                {
                    tokens[scope] = token;
                }
                else if (IsRunWide(attempt.Outcome))
                {
                    // Spec I-5: the key or the account is wrong for every scope; six identical failures say nothing more.
                    runWide = attempt.Outcome;
                }
            }
            catch (OperationCanceledException) when (limit.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                timedOut = true;
                steps.Add(new AccessCheckStep(AccessCheckStepKind.Delegation, scope, AccessCheckStepOutcome.GoogleUnavailable, null));
                delegation[scope] = AccessCheckStepOutcome.GoogleUnavailable;
            }
        }

        steps.Add(await ReadAsync(
            AccessCheckStepKind.ClassroomRead,
            GoogleDelegationScopes.CoursesReadonly,
            probe.ReadCoursesAsync));
        steps.Add(await ReadAsync(
            AccessCheckStepKind.ReportsRead,
            GoogleDelegationScopes.AdminReportsAuditReadonly,
            probe.ReadMeetActivityAsync));
        return AccessCheckResult.Of(steps);

        // Spec FR-003: a read runs only when the delegation of its own scope succeeded; its token is used and dropped.
        async Task<AccessCheckStep> ReadAsync(
            AccessCheckStepKind kind,
            string scope,
            Func<DelegatedToken, CancellationToken, Task<AccessCheckStepOutcome>> read)
        {
            if (timedOut)
            {
                return new AccessCheckStep(kind, null, AccessCheckStepOutcome.GoogleUnavailable, null);
            }

            if (runWide is { } cause)
            {
                return NotAttempted(kind, null, cause);
            }

            if (!tokens.TryGetValue(scope, out var token))
            {
                return NotAttempted(kind, null, delegation[scope]);
            }

            try
            {
                return new AccessCheckStep(kind, null, await read(token, linked.Token), null);
            }
            catch (OperationCanceledException) when (limit.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                timedOut = true;
                return new AccessCheckStep(kind, null, AccessCheckStepOutcome.GoogleUnavailable, null);
            }
        }
    }

    /// <summary>Spec FR-005: the causes that are the same for every scope.</summary>
    public static bool IsRunWide(AccessCheckStepOutcome outcome) =>
        outcome is AccessCheckStepOutcome.KeyUnavailable
            or AccessCheckStepOutcome.KeyRejected
            or AccessCheckStepOutcome.TechnicalAccountUnknown;

    private static AccessCheckStep NotAttempted(AccessCheckStepKind kind, string? scope, AccessCheckStepOutcome because) =>
        new(kind, scope, AccessCheckStepOutcome.NotAttempted, because);
}
