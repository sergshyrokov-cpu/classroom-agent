using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The translation keys of the two US-012 areas (spec FR-020, AC-015). Every user-visible string comes from
/// here; none is written in a controller or a view (NFR-073).
/// </summary>
public static class DeanAccountTextKeys
{
    public const string Title = "DeanAccounts.Title";

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

    public const string SignInRefused = "SignIn.Refused.WrongLoginOrPassword";

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

    /// <summary>The message of a refused management action (spec FR-020).</summary>
    public static string Of(DeanAccountRefusal refusal) => refusal switch
    {
        DeanAccountRefusal.NotAnEmailAddress => "DeanAccounts.Refused.NotAnEmail",
        DeanAccountRefusal.OutsideSchoolDomain => "DeanAccounts.Refused.OutsideSchoolDomain",
        DeanAccountRefusal.EmailAlreadyUsed => "DeanAccounts.Refused.EmailAlreadyUsed",
        DeanAccountRefusal.AlreadyInThatState => "DeanAccounts.Refused.AlreadyInThatState",
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, null),
    };

    /// <summary>The message of a password the policy refused (spec FR-005, VR-002).</summary>
    public static string Of(PasswordPolicyViolation violation) => violation switch
    {
        PasswordPolicyViolation.TooShort => "Password.Refused.TooShort",
        PasswordPolicyViolation.TooLong => "Password.Refused.TooLong",
        PasswordPolicyViolation.EqualsLogin => "Password.Refused.EqualsLogin",
        PasswordPolicyViolation.ContainsLogin => "Password.Refused.ContainsLogin",
        PasswordPolicyViolation.EqualsTemporaryPassword => "Password.Refused.EqualsTemporary",
        _ => throw new ArgumentOutOfRangeException(nameof(violation), violation, null),
    };
}
