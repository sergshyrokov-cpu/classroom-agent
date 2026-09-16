using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Localization;

/// <summary>AC-009, TC-8: every Control Plane translation key exists in Ukrainian and English.</summary>
public sealed class TranslationCompletenessTests(PostgreSqlFixture database)
{
    /// <summary>The message keys fixed by the API contract (api-design §7, openapi FieldMessageKeys).</summary>
    private static readonly string[] Keys =
    [
        "Setup.Login.Required",
        "Setup.Login.Length",
        "Setup.Login.Characters",
        "Setup.Password.Required",
        "Setup.Password.Length",
        "Setup.Password.ContainsLogin",
        "Setup.PasswordConfirmation.Required",
        "Setup.PasswordConfirmation.Mismatch",
        "Setup.SetupCode.Invalid",
        "Setup.AlreadyCreated",
        "SignIn.Login.Required",
        "SignIn.Password.Required",
        "SignIn.Refused",
        "Error.PageExpired",
        "Error.Forbidden",
        "Error.NotFound",
        "Error.Internal",
    ];

    public static TheoryData<string> ContractKeys => new(Keys);

    [Fact]
    public async Task EveryKey_ExistsInUkrainianAndEnglish()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var ukrainian = host.AllTexts("uk");
        var english = host.AllTexts("en");

        Assert.NotEmpty(ukrainian);
        Assert.Equal(ukrainian.Keys.Order(StringComparer.Ordinal), english.Keys.Order(StringComparer.Ordinal));
        Assert.All(ukrainian, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value), $"uk: {pair.Key}"));
        Assert.All(english, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value), $"en: {pair.Key}"));
        Assert.All(Keys, key => Assert.Contains(key, ukrainian.Keys));
    }

    [Theory]
    [MemberData(nameof(ContractKeys))]
    public async Task ContractMessageKeys_AreResolvedFromLocalization(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var ukrainian = host.Text(key, "uk");
        var english = host.Text(key, "en");

        Assert.NotEqual(ukrainian, english);
    }
}
