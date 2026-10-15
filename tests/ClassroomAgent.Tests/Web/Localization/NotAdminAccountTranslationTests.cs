using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-051 FR-006, NFR-073: the "this address is a Dean account" refusal comes from the translation files in Ukrainian
/// and English, with the texts the Specification fixes, and differs from every other sign-in refusal (TC-8).
/// </summary>
public sealed class NotAdminAccountTranslationTests(PostgreSqlFixture database)
{
    private const string Key = SignInTestData.TextKeys.RefusedNotAdminAccount;

    /// <summary>FR-006: the texts of the specification's table, exactly.</summary>
    [Theory]
    [InlineData("uk", "Ця адреса — обліковий запис декана. Увійдіть за логіном і паролем. Для ролі адміністратора потрібна інша адреса.")]
    [InlineData("en", "This address is a Dean account. Sign in with your login and password. The Admin role needs a different address.")]
    public async Task TheKey_HasTheSpecifiedTextInEachLanguage(string culture, string expected)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        Assert.Equal(expected, host.Text(Key, culture));
    }

    /// <summary>FR-006: a refusal of its own — not the text of "not approved", "could not confirm", "failed" or "disabled".</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheText_DiffersFromEveryOtherSignInRefusal(string culture)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var text = host.Text(Key, culture);

        Assert.NotEqual(host.Text(SignInTestData.TextKeys.RefusedNotApproved, culture), text);
        Assert.NotEqual(host.Text(SignInTestData.TextKeys.RefusedCouldNotConfirm, culture), text);
        Assert.NotEqual(host.Text(SignInTestData.TextKeys.RefusedSignInFailed, culture), text);
        Assert.NotEqual(host.Text(SignInTestData.TextKeys.RefusedAccountDisabled, culture), text);
    }
}
