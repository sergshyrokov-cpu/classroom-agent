using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The log lines of the sign-in path, at the levels US-008 spec FR-020 fixes. Every line carries internal
/// identifiers, categories, outcomes and states only — never an email, a token, an authorization code, a
/// <c>state</c> value, a cookie, the client id, the secret reference or a response body (SC-10, DC-10).
/// </summary>
/// <remarks>
/// The request id is written as a property of its own, so an audit row and its log line can be tied together
/// (SC-11): the row stores the same value in <c>request_id</c>.
/// </remarks>
public static class SignInLog
{
    private static readonly EventId Succeeded = new(2200, "AdminSignInSucceeded");

    private static readonly EventId NotApproved = new(2201, "AdminSignInNotApproved");

    private static readonly EventId CouldNotConfirm = new(2202, "AdminSignInCouldNotConfirm");

    private static readonly EventId CallbackFailed = new(2203, "AdminSignInCallbackFailed");

    private static readonly EventId AccountDisabled = new(2204, "AdminSignInAccountDisabled");

    public static void Succeed(ILogger logger, long appUserId, string? requestId) =>
        logger.LogInformation(
            Succeeded,
            "Admin sign-in succeeded for account {AppUserId} in request {RequestId}",
            appUserId,
            requestId);

    /// <summary>
    /// FR-020: a refusal that is a decision — not approved, a failed callback, a disabled account — is a
    /// <c>Warning</c>; one that means the Control Plane could not be asked is an <c>Error</c>, because it needs an
    /// operator. Only the category is written, never what was entered.
    /// </summary>
    public static void Refuse(ILogger logger, SignInRefusal refusal, string? requestId)
    {
        switch (refusal)
        {
            case SignInRefusal.CouldNotConfirm:
                logger.LogError(
                    CouldNotConfirm,
                    "Admin sign-in refused in request {RequestId}: the approval could not be confirmed",
                    requestId);
                break;
            case SignInRefusal.NotApproved:
                logger.LogWarning(
                    NotApproved,
                    "Admin sign-in refused in request {RequestId}: not an approved Admin of this installation",
                    requestId);
                break;
            case SignInRefusal.SignInFailed:
                logger.LogWarning(
                    CallbackFailed,
                    "Admin sign-in refused in request {RequestId}: the callback itself failed",
                    requestId);
                break;
            case SignInRefusal.AccountDisabled:
                logger.LogWarning(
                    AccountDisabled,
                    "Admin sign-in refused in request {RequestId}: the account is disabled",
                    requestId);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(refusal), refusal, null);
        }
    }
}
