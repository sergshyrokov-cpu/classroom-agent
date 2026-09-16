namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>Synthetic values shared by the US-001 tests (TC-4: nothing real).</summary>
public static class TestData
{
    /// <summary>26 Crockford Base32 characters in groups of 4 or 5 (OD-005).</summary>
    public const string SetupCode = "01234-56789-ABCD-EFGH-JKMN-PQRS";

    /// <summary>A second valid code, for the restart scenario.</summary>
    public const string OtherSetupCode = "ZYXWV-TSRQP-NMKJ-HGFE-DCBA-9876";

    public const string Login = "owner.one";

    public const string Password = "correct horse battery staple";

    public static IReadOnlyList<KeyValuePair<string, string>> SetupFields(
        string? setupCode = SetupCode,
        string login = Login,
        string password = Password,
        string? passwordConfirmation = null) =>
    [
        new("setupCode", setupCode ?? string.Empty),
        new("login", login),
        new("password", password),
        new("passwordConfirmation", passwordConfirmation ?? password),
    ];

    public static IReadOnlyList<KeyValuePair<string, string>> SignInFields(string login = Login, string password = Password) =>
    [
        new("login", login),
        new("password", password),
    ];
}
