namespace ClassroomAgent.Web.Security;

/// <summary>
/// Tells the failures of a Google callback apart (US-077 spec FR-006, I-5). They share one exception type, so the
/// framework's fixed messages are compared here, in memory; neither a message nor <c>Data</c> is written or carried
/// further (S-04). The order matters: a token-endpoint failure with a standard error body also carries
/// <c>Data["error"]</c>.
/// </summary>
internal static class GoogleSignInFailureClassifier
{
    private const string CorrelationFailed = "Correlation failed.";
    private const string StateInvalid = "The oauth state was missing or invalid.";
    private const string TokenEndpointFailure = "OAuth token endpoint failure";
    private const string NoAccessToken = "Failed to retrieve access token.";
    private const string AccessDenied = "Access was denied by the resource owner or by the remote server.";
    private const string ErrorDataKey = "error";

    public static GoogleSignInFailureKind Classify(Exception? failure)
    {
        if (failure is null)
        {
            return GoogleSignInFailureKind.Other;
        }

        var message = failure.Message;
        if (message is CorrelationFailed or StateInvalid)
        {
            return GoogleSignInFailureKind.CorrelationFailed;
        }

        if (message.StartsWith(TokenEndpointFailure, StringComparison.Ordinal) || message == NoAccessToken)
        {
            return GoogleSignInFailureKind.TokenExchangeFailed;
        }

        return failure.Data.Contains(ErrorDataKey) || message == AccessDenied
            ? GoogleSignInFailureKind.GoogleError
            : GoogleSignInFailureKind.Other;
    }
}
