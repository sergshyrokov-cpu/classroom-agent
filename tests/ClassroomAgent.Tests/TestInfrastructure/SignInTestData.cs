namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Paths, cookie names, translation keys and synthetic emails of the installation sign-in
/// (US-008 openapi paths and <c>PageTextKeys</c>). Every value is fixed by the approved contract; a test
/// that needs a different one is testing something the contract does not say.
/// </summary>
public static class SignInTestData
{
    public const string SignInPath = "/sign-in";

    public const string StartPath = "/sign-in/google";

    public const string CallbackPath = "/signin-google";

    public const string SignOutPath = "/sign-out";

    public const string LandingPath = "/";

    public const string SessionCookieName = "__Host-ca-session";

    public const string AntiforgeryCookieName = "__Host-ca-antiforgery";

    /// <summary>An approved Admin in the installation's own domain (TC-4: synthetic).</summary>
    public const string AdminEmail = "ivan.petrenko@school-one.example.test";

    /// <summary>A second approved Admin — BR-013 asks for at least two per school.</summary>
    public const string SecondAdminEmail = "olena.koval@school-one.example.test";

    /// <summary>An email the Owner never approved, in the school's domain.</summary>
    public const string UnapprovedEmail = "stranger@school-one.example.test";

    /// <summary>An account outside the school's domain — the domain hint is not the access decision (S-07).</summary>
    public const string OutsideDomainEmail = "someone@other-school.example.test";

    /// <summary>The same address as <see cref="AdminEmail"/> in mixed case, as Google may return it (BR-079).</summary>
    public const string AdminEmailMixedCase = "Ivan.Petrenko@School-One.Example.Test";

    public static string ErrorPath(int statusCode) => $"/error/{statusCode}";

    public static class TextKeys
    {
        public const string SignInTitle = "SignIn.Title";

        public const string GoogleButton = "SignIn.Google.Button";

        public const string RefusedNotApproved = "SignIn.Refused.NotApproved";

        public const string RefusedCouldNotConfirm = "SignIn.Refused.CouldNotConfirm";

        public const string RefusedSignInFailed = "SignIn.Refused.SignInFailed";

        public const string RefusedAccountDisabled = "SignIn.Refused.AccountDisabled";

        public const string LandingTitle = "Landing.Title";

        public const string SignedInAs = "Landing.SignedInAs";

        public const string RoleAdmin = "Landing.Role.Admin";

        public const string RoleDean = "Landing.Role.Dean";

        public const string SignOut = "Landing.SignOut";

        public const string ReadOnlyTitle = "Landing.ReadOnly.Title";

        public const string ReadOnlyNotYetConfirmed = "Landing.ReadOnly.NotYetConfirmed";

        public const string ReadOnlySuspended = "Landing.ReadOnly.SuspendedByOwner";

        public const string ReadOnlyGracePeriodExpired = "Landing.ReadOnly.GracePeriodExpired";

        public const string ReadOnlyLastSuccessfulCheck = "Landing.ReadOnly.LastSuccessfulCheck";

        public const string LegitimacyOk = "Landing.Legitimacy.Ok";

        public const string PageExpired = "Error.PageExpired";

        public const string Forbidden = "Error.Forbidden";

        public const string NotFound = "Error.NotFound";

        public const string Unexpected = "Error.Unexpected";

        public const string RefusedNotYetConfirmed = "ReadOnly.Refused.NotYetConfirmed";

        public const string RefusedSuspendedByOwner = "ReadOnly.Refused.SuspendedByOwner";

        public const string RefusedGracePeriodExpired = "ReadOnly.Refused.GracePeriodExpired";

        /// <summary>Every key US-008 openapi <c>PageTextKeys</c> declares (spec FR-017, AC-015).</summary>
        public static readonly string[] All =
        [
            SignInTitle,
            GoogleButton,
            RefusedNotApproved,
            RefusedCouldNotConfirm,
            RefusedSignInFailed,
            RefusedAccountDisabled,
            LandingTitle,
            SignedInAs,
            RoleAdmin,
            RoleDean,
            SignOut,
            ReadOnlyTitle,
            ReadOnlyNotYetConfirmed,
            ReadOnlySuspended,
            ReadOnlyGracePeriodExpired,
            ReadOnlyLastSuccessfulCheck,
            LegitimacyOk,
            PageExpired,
            Forbidden,
            NotFound,
            Unexpected,
            RefusedNotYetConfirmed,
            RefusedSuspendedByOwner,
            RefusedGracePeriodExpired,
        ];
    }

    /// <summary>The refusal categories of <c>audit_event.refusal_category</c> (spec FR-012; db-design 4.1).</summary>
    public static class RefusalCategories
    {
        public const string NotInAllowedAdmin = "not_in_allowed_admin";

        public const string ControlPlaneUnavailable = "control_plane_unavailable";

        public const string UnknownInstallation = "unknown_installation";

        public const string CallbackFailed = "callback_failed";

        public const string AccountDisabled = "account_disabled";
    }
}
