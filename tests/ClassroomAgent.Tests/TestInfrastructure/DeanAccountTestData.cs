using ClassroomAgent.Application.Authorization;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Everything the US-012 tests and the future implementation must agree on: paths, policy names, form field
/// names, translation keys, audit values and the synthetic accounts and passwords. One place, so the tests and
/// the screens cannot drift (the US-009 / US-011 pattern).
/// </summary>
public static class DeanAccountTestData
{
    /// <summary>The school's domain, as the legitimacy state reports it. Synthetic (TC-4).</summary>
    public const string Domain = "school-one.example.test";

    public const string DeanEmail = "dean@school-one.example.test";

    public const string DeanEmailMixedCase = "Dean@School-One.Example.Test";

    public const string SecondDeanEmail = "second.dean@school-one.example.test";

    public const string OutsideDomainEmail = "dean@gmail.example.test";

    /// <summary>A password that satisfies SC-2: 15 to 128 characters, no composition rule, spaces allowed.</summary>
    public const string TemporaryPassword = "temporary pass one";

    public const string NewPassword = "a quite different one";

    public const string WrongPassword = "not the stored one at all";

    /// <summary>Exactly <see cref="ClassroomAgent.Application.UseCases.DeanPasswordPolicy.MinimumLength"/>.</summary>
    public const string ShortestAllowedPassword = "123456789012345";

    /// <summary>One character short of the minimum.</summary>
    public const string TooShortPassword = "12345678901234";

    public static class Paths
    {
        public const string Deans = "/settings/deans";

        public const string SignIn = "/sign-in";

        public const string ForcedChange = "/sign-in/change-password";

        public const string OwnPassword = "/account/password";

        public const string Landing = "/";

        public static string State(long deanId) => $"{Deans}/{deanId}/state";

        public static string Password(long deanId) => $"{Deans}/{deanId}/password";
    }

    public static class Policies
    {
        public const string Manage = InstallationPolicies.ManageDeanAccounts;

        public const string ChangeOwn = InstallationPolicies.ChangeOwnPassword;

        public const string CompleteTemporary = InstallationPolicies.CompleteTemporaryPasswordChange;
    }

    /// <summary>Form field names of the api-design schemas.</summary>
    public static class Fields
    {
        public const string Email = "email";

        public const string TemporaryPassword = "temporaryPassword";

        public const string Password = "password";

        public const string DesiredState = "desiredState";

        public const string CurrentPassword = "currentPassword";

        public const string NewPassword = "newPassword";

        public const string StateActive = "Active";

        public const string StateDisabled = "Disabled";
    }

    /// <summary>The database codes of db-design §4.</summary>
    public static class Audit
    {
        public const string Created = "dean_account_created";

        public const string Disabled = "dean_account_disabled";

        public const string ReEnabled = "dean_account_reenabled";

        public const string PasswordReset = "dean_account_password_reset";

        public const string PasswordChanged = "dean_password_changed";

        public const string SignIn = "dean_sign_in";

        public const string TargetType = "app_user";

        public const string UnknownLogin = "unknown_login";

        public const string WrongPassword = "wrong_password";

        public const string LockedOut = "locked_out";

        public const string AccountDisabled = "account_disabled";

        public const string ReadOnlyMode = "read_only_mode";

        public static readonly string[] AllActions =
        [
            Created,
            Disabled,
            ReEnabled,
            PasswordReset,
            PasswordChanged,
            SignIn,
        ];
    }

    /// <summary>
    /// Every user-visible string the two areas need (spec FR-020, AC-015). The list is the contract: a key that
    /// is missing from a translation file fails a test rather than a browser.
    /// </summary>
    public static class TextKeys
    {
        public const string ListTitle = "DeanAccounts.Title";

        public const string ColumnEmail = "DeanAccounts.Column.Email";

        public const string ColumnState = "DeanAccounts.Column.State";

        public const string ColumnTemporary = "DeanAccounts.Column.PasswordIsTemporary";

        public const string ColumnLastSignIn = "DeanAccounts.Column.LastSignIn";

        public const string StateActive = "DeanAccounts.State.Active";

        public const string StateDisabled = "DeanAccounts.State.Disabled";

        public const string CreateSubmit = "DeanAccounts.Create.Submit";

        public const string CreateEmailLabel = "DeanAccounts.Create.Email.Label";

        public const string CreatePasswordLabel = "DeanAccounts.Create.TemporaryPassword.Label";

        public const string ActionDisable = "DeanAccounts.Action.Disable";

        public const string ActionReEnable = "DeanAccounts.Action.ReEnable";

        public const string ActionResetPassword = "DeanAccounts.Action.ResetPassword";

        public const string CreatedConfirmation = "DeanAccounts.Confirmation.Created";

        public const string DisabledConfirmation = "DeanAccounts.Confirmation.Disabled";

        public const string ReEnabledConfirmation = "DeanAccounts.Confirmation.ReEnabled";

        public const string PasswordResetConfirmation = "DeanAccounts.Confirmation.PasswordReset";

        public const string RefusedNotAnEmail = "DeanAccounts.Refused.NotAnEmail";

        public const string RefusedOutsideDomain = "DeanAccounts.Refused.OutsideSchoolDomain";

        public const string RefusedEmailInUse = "DeanAccounts.Refused.EmailAlreadyUsed";

        public const string RefusedAlreadyInThatState = "DeanAccounts.Refused.AlreadyInThatState";

        public const string PasswordTooShort = "Password.Refused.TooShort";

        public const string PasswordTooLong = "Password.Refused.TooLong";

        public const string PasswordEqualsLogin = "Password.Refused.EqualsLogin";

        public const string PasswordContainsLogin = "Password.Refused.ContainsLogin";

        public const string PasswordEqualsTemporary = "Password.Refused.EqualsTemporary";

        /// <summary>The ONE message steps 1, 2 and 3 of the sequence share (spec I-6, S-05).</summary>
        public const string SignInRefused = "SignIn.Refused.WrongLoginOrPassword";

        /// <summary>Step 4 only, and only with the correct password and no lockout (SC-2 v65).</summary>
        public const string SignInAccountDisabled = "SignIn.Refused.AccountDisabled";

        public const string SignInEmailLabel = "SignIn.Password.Email.Label";

        public const string SignInPasswordLabel = "SignIn.Password.Password.Label";

        public const string SignInSubmit = "SignIn.Password.Submit";

        public const string ForcedChangeTitle = "Password.ForcedChange.Title";

        public const string ForcedChangeExplanation = "Password.ForcedChange.Explanation";

        public const string OwnPasswordTitle = "Password.Own.Title";

        public const string OwnPasswordCurrentLabel = "Password.Own.Current.Label";

        public const string OwnPasswordNewLabel = "Password.Own.New.Label";

        public const string OwnPasswordSubmit = "Password.Own.Submit";

        public const string OwnPasswordChanged = "Password.Own.Changed";

        public const string OwnPasswordWrongCurrent = "Password.Own.Refused.WrongCurrent";

        public static readonly string[] All =
        [
            ListTitle,
            ColumnEmail,
            ColumnState,
            ColumnTemporary,
            ColumnLastSignIn,
            StateActive,
            StateDisabled,
            CreateSubmit,
            CreateEmailLabel,
            CreatePasswordLabel,
            ActionDisable,
            ActionReEnable,
            ActionResetPassword,
            CreatedConfirmation,
            DisabledConfirmation,
            ReEnabledConfirmation,
            PasswordResetConfirmation,
            RefusedNotAnEmail,
            RefusedOutsideDomain,
            RefusedEmailInUse,
            RefusedAlreadyInThatState,
            PasswordTooShort,
            PasswordTooLong,
            PasswordEqualsLogin,
            PasswordContainsLogin,
            PasswordEqualsTemporary,
            SignInRefused,
            SignInAccountDisabled,
            SignInEmailLabel,
            SignInPasswordLabel,
            SignInSubmit,
            ForcedChangeTitle,
            ForcedChangeExplanation,
            OwnPasswordTitle,
            OwnPasswordCurrentLabel,
            OwnPasswordNewLabel,
            OwnPasswordSubmit,
            OwnPasswordChanged,
            OwnPasswordWrongCurrent,
        ];
    }
}
