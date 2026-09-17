namespace ClassroomAgent.Application.Models;

/// <summary>
/// Why a legitimacy check was unsuccessful (US-005 spec FR-007; test strategy §3): the client categories of
/// <see cref="CheckFailureCategory"/>, an <c>upgrade_required</c> answer, a failed save of
/// <c>LegitimacyState</c>, or an unexpected error inside the check.
/// </summary>
public enum LegitimacyCheckFailure
{
    Unreachable,
    Timeout,
    ErrorAnswer,
    UnparseableAnswer,
    UnknownInstallation,
    UpgradeRequired,
    SaveFailed,
    UnexpectedError,
}
