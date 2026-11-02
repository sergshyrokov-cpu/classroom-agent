using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.PasswordFieldMarkup;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>
/// US-069 AC-005, AC-006, AC-007, AC-008 on the Control Plane's three password fields (spec FR-005, FR-007): what the
/// server renders. These fields have a <c>label for</c> beside them, so no label wraps the wrapper; each keeps its
/// id and its <c>value=""</c>.
/// </summary>
public sealed class ControlPlanePasswordToggleMarkupTests(PostgreSqlFixture database)
{
    private static readonly ExpectedField Password = new("password", null, string.Empty);

    private static readonly ExpectedField PasswordConfirmation = new("passwordConfirmation", null, string.Empty);

    private async Task<ControlPlaneTestHost> StartWithOwnerAsync(CancellationToken ct)
    {
        var host = await ControlPlaneTestHost.StartAsync(database, ct);
        (await host.CreateOwnerAsync(ct)).Dispose();
        return host;
    }

    /// <summary>AC-005, AC-006, FR-005.1…FR-005.3: the two setup fields, before the Owner account exists.</summary>
    [Fact]
    public async Task TheSetupFields_CarryTheControl()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.GetAsync("/setup", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(2, PasswordInputs(page.Body).Count);
        var fields = Fields(page.Body);
        Assert.Equal(2, fields.Count);
        AssertField(fields[0], Password, "uk");
        AssertField(fields[1], PasswordConfirmation, "uk");
        Assert.Equal("password", Value(fields[0].Input, "id"));
        Assert.Equal("passwordConfirmation", Value(fields[1].Input, "id"));
    }

    /// <summary>AC-005, AC-006, FR-005.1…FR-005.3: the sign-in field.</summary>
    [Fact]
    public async Task TheSignInField_CarriesTheControl()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWithOwnerAsync(ct);
        using var client = host.CreateClient();

        var page = await client.GetAsync("/sign-in", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Single(PasswordInputs(page.Body));
        var field = Assert.Single(Fields(page.Body));
        AssertField(field, Password, "uk");
        Assert.Equal("password", Value(field.Input, "id"));
    }

    /// <summary>AC-007, FR-006.1, FR-007: nothing marks a field ready in the markup.</summary>
    [Theory]
    [InlineData("/setup", false)]
    [InlineData("/sign-in", true)]
    public async Task WithoutTheScript_NoFieldIsReady_AndEveryControlIsHidden(string path, bool withOwner)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = withOwner ? await StartWithOwnerAsync(ct) : await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.GetAsync(path, ct);

        Assert.DoesNotContain(ReadyClass, page.Body, StringComparison.Ordinal);
        var fields = Fields(page.Body);
        Assert.NotEmpty(fields);
        Assert.All(fields, f => Assert.True(HasAttribute(f.Control, "hidden"), f.Control));
    }

    /// <summary>AC-008, FR-005.2: each form names exactly what it names today. Green before and after by design.</summary>
    [Theory]
    [InlineData("/setup", false, new[] { Html.AntiforgeryFieldName, "setupCode", "login", "password", "passwordConfirmation" })]
    [InlineData("/sign-in", true, new[] { Html.AntiforgeryFieldName, "login", "password" })]
    public async Task TheControl_AddsNoFieldToTheForm(string path, bool withOwner, string[] expected)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = withOwner ? await StartWithOwnerAsync(ct) : await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.GetAsync(path, ct);

        Assert.Equal(expected, NamedControls(Assert.Single(FormsWithAPasswordField(page.Body))));
    }

    /// <summary>AC-008, FR-005.5: the page references the same-host script once, deferred.</summary>
    [Theory]
    [InlineData("/setup", false)]
    [InlineData("/sign-in", true)]
    public async Task ThePage_ReferencesTheScriptOnce_Deferred(string path, bool withOwner)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = withOwner ? await StartWithOwnerAsync(ct) : await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var page = await client.GetAsync(path, ct);

        var script = Assert.Single(ScriptReferences(page.Body));
        Assert.True(HasAttribute(script, "defer"), script);
    }

    /// <summary>FR-005.5: not from the layout — the Owner's home page does not load it.</summary>
    [Fact]
    public async Task APageWithNoPasswordField_DoesNotLoadTheScript()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var page = await owner.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Empty(ScriptReferences(page.Body));
    }

    /// <summary>Spec Error Handling: a field re-rendered after a refused sign-in starts hidden, its <c>value=""</c> kept.</summary>
    [Fact]
    public async Task AfterARefusedSignIn_TheFieldStartsHidden()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWithOwnerAsync(ct);

        var (client, page) = await host.SignInAsync(TestData.Login, "not the stored password at all", ct);
        using var _client = client;

        Assert.Equal(HttpStatusCode.Unauthorized, page.Status);
        Assert.Single(PasswordInputs(page.Body));
        AssertField(Assert.Single(Fields(page.Body)), Password, "uk");
    }

    /// <summary>Spec Error Handling: the same for the setup form refused for a wrong code.</summary>
    [Fact]
    public async Task AfterARefusedSetup_TheFieldsStartHidden()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();
        await client.GetAsync("/setup", ct);

        var page = await client.PostFormAsync("/setup", TestData.SetupFields(TestData.OtherSetupCode), ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Equal(2, PasswordInputs(page.Body).Count);
        var fields = Fields(page.Body);
        Assert.Equal(2, fields.Count);
        AssertField(fields[0], Password, "uk");
        AssertField(fields[1], PasswordConfirmation, "uk");
    }

    /// <summary>AC-008, SC-4, FR-008.1: the script and both icons are served anonymously, with their content types.</summary>
    [Theory]
    [InlineData(ScriptPath, "javascript")]
    [InlineData(EyeIconPath, "image/svg+xml")]
    [InlineData(EyeOffIconPath, "image/svg+xml")]
    public async Task AStaticFile_IsServedWithItsContentType(string path, string contentType)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        var response = await anonymous.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Contains(contentType, Styling.HeaderOf(response, "Content-Type") ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
