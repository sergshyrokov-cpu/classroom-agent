using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The log lines of "Проверить доступ" and of the startup self-check (US-011 spec FR-010, FR-014; DC-10). Every line
/// carries categories, the verdict and scope URIs only — never the technical account, the domain, the key, its
/// reference, a token, or anything Google said (SC-7, SC-10).
/// </summary>
public static class AccessCheckLog
{
    private static readonly EventId RunEvent = new(2400, "AccessCheckRun");

    private static readonly EventId RefusedEvent = new(2401, "AccessCheckRefused");

    private static readonly EventId StepFailedEvent = new(2402, "AccessCheckStepFailed");

    private static readonly EventId SelfCheckCompletedEvent = new(2403, "AccessSelfCheckCompleted");

    private static readonly EventId SelfCheckSkippedEvent = new(2404, "AccessSelfCheckSkipped");

    private static readonly EventId SelfCheckFailedEvent = new(2405, "AccessSelfCheckFailed");

    public static void Run(ILogger logger, long appUserId, AccessCheckResult result, string? requestId)
    {
        logger.LogInformation(
            RunEvent,
            "Access check run by account {AppUserId} in request {RequestId}: {Verdict}",
            appUserId,
            requestId,
            result.Verdict);
        foreach (var step in result.Steps.Where(s => AccessCheckResult.IsConfigurationFailure(s.Outcome)))
        {
            logger.LogWarning(
                StepFailedEvent,
                "Access check step {Step} {Scope} in request {RequestId}: {Outcome}",
                step.Kind,
                step.Scope,
                requestId,
                step.Outcome);
        }
    }

    public static void Refused(ILogger logger, AccessCheckRefusal refusal, string? requestId) =>
        logger.LogWarning(RefusedEvent, "Access check refused in request {RequestId}: {Refusal}", requestId, refusal);

    /// <summary>DC-10: Information when access is in place, Error otherwise — what the Owner reads after a key rotation.</summary>
    public static void SelfCheckCompleted(ILogger logger, AccessCheckResult result)
    {
        var steps = string.Join(
            "; ",
            result.Steps.Select(s => s.Scope is null ? $"{s.Kind}={s.Outcome}" : $"{s.Kind} {s.Scope}={s.Outcome}"));
        var level = result.Verdict == AccessCheckVerdict.AccessInPlace ? LogLevel.Information : LogLevel.Error;
        logger.Log(level, SelfCheckCompletedEvent, "Startup access self-check: {Verdict} ({Steps})", result.Verdict, steps);
    }

    /// <summary>OD-004, spec I-6: a skip is said, at Warning, so silence is never mistaken for success.</summary>
    public static void SelfCheckSkipped(ILogger logger, string reason) =>
        logger.LogWarning(SelfCheckSkippedEvent, "Startup access self-check skipped: {Reason}", reason);

    public static void SelfCheckFailed(ILogger logger, Exception failure) =>
        logger.LogError(SelfCheckFailedEvent, "Startup access self-check could not complete: {Failure}", failure.GetType().Name);
}
