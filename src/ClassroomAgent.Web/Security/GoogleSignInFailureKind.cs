namespace ClassroomAgent.Web.Security;

/// <summary>Why a Google sign-in callback failed: the closed list of US-077 spec FR-006, VR-003.</summary>
public enum GoogleSignInFailureKind
{
    CorrelationFailed,
    TokenExchangeFailed,
    GoogleError,
    Other,
}
