namespace ClassroomAgent.ControlPlane.Push;

/// <summary>
/// The result of one push attempt and, for <see cref="StatusPushAttemptResult.UnexpectedStatus"/>, the status
/// code that was answered (US-006 api-design §6, §9). No response body is ever carried (S-10).
/// </summary>
public sealed record StatusPushAttempt(StatusPushAttemptResult Result, int? StatusCode = null)
{
    /// <summary>A delivered or refused attempt ends the push; every other result is retried.</summary>
    public bool IsFinal => Result is StatusPushAttemptResult.Delivered or StatusPushAttemptResult.Refused;
}
