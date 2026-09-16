using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-006: setup field rules VR-001, VR-002, VR-003 and VR-007, checked before the code (OD-004).</summary>
public sealed class SetupValidationTests(PostgreSqlFixture database)
{
    [Theory]
    [InlineData("abcd", null)]
    [InlineData("a.b-c_9", null)]
    [InlineData("A234567890123456789012345678901234567890123456789012345678901234", null)]
    [InlineData("abc", "Setup.Login.Length")]
    [InlineData("A2345678901234567890123456789012345678901234567890123456789012345", "Setup.Login.Length")]
    [InlineData("own er", "Setup.Login.Characters")]
    [InlineData("владелец", "Setup.Login.Characters")]
    [InlineData("owner@x", "Setup.Login.Characters")]
    [InlineData("", "Setup.Login.Required")]
    public async Task Login_Boundaries(string login, string? expectedKey)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var response = await PostSetupAsync(host, TestData.SetupFields(login: login), ct);

        AssertOutcome(host, response, expectedKey);
    }

    [Theory]
    [InlineData("x", 14, "Setup.Password.Length")]
    [InlineData("x", 15, null)]
    [InlineData("x", 128, null)]
    [InlineData("x", 129, "Setup.Password.Length")]
    [InlineData("п", 15, null)]
    [InlineData("😀", 15, null)]
    [InlineData("😀", 128, null)]
    [InlineData("😀", 129, "Setup.Password.Length")]
    public async Task Password_LengthBoundaries_CountedInCodePoints(string character, int count, string? expectedKey)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var password = string.Concat(Enumerable.Repeat(character, count));

        var response = await PostSetupAsync(host, TestData.SetupFields(password: password), ct);

        AssertOutcome(host, response, expectedKey);
    }

    [Fact]
    public async Task Password_FifteenLowercaseLettersWithSpaces_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var response = await PostSetupAsync(host, TestData.SetupFields(password: "abc def ghi jkl"), ct);

        AssertOutcome(host, response, expectedKey: null);
    }

    [Theory]
    [InlineData("OWNER.ONE")]
    [InlineData("the owner.one password is long")]
    [InlineData("The OWNER.One password is long")]
    public async Task Password_EqualToOrContainingLoginInAnyCase_IsRejected(string password)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var response = await PostSetupAsync(host, TestData.SetupFields(password: password), ct);

        AssertOutcome(host, response, "Setup.Password.ContainsLogin");
        Assert.Equal(0, await host.OwnerCountAsync(ct));
    }

    [Theory]
    [InlineData("correct horse battery stapla", "Setup.PasswordConfirmation.Mismatch")]
    [InlineData("Correct horse battery staple", "Setup.PasswordConfirmation.Mismatch")]
    [InlineData("correct horse battery staple ", "Setup.PasswordConfirmation.Mismatch")]
    [InlineData("", "Setup.PasswordConfirmation.Required")]
    public async Task Confirmation_Mismatch_IsRejected(string confirmation, string expectedKey)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var fields = TestData.SetupFields()
            .Select(f => f.Key == "passwordConfirmation" ? new KeyValuePair<string, string>(f.Key, confirmation) : f);

        var response = await PostSetupAsync(host, fields, ct);

        AssertOutcome(host, response, expectedKey);
        Assert.Equal(0, await host.OwnerCountAsync(ct));
    }

    [Fact]
    public async Task SeveralInvalidFields_AllReportedTogether()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var response = await PostSetupAsync(
            host,
            TestData.SetupFields(login: "ab", password: "short", passwordConfirmation: "other"),
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text("Setup.Login.Length", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("Setup.Password.Length", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("Setup.PasswordConfirmation.Mismatch", "uk"), response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidationFailure_DoesNotEchoPasswordConfirmationOrCode()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        const string password = "a secret password 123";
        const string confirmation = "a different secret 456";

        var response = await PostSetupAsync(
            host,
            TestData.SetupFields(login: "ab", password: password, passwordConfirmation: confirmation),
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.DoesNotContain(password, response.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(confirmation, response.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.CodeGenerator.Code, response.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("ab", Html.InputValue(response.Body, "login"));
        Assert.Equal(string.Empty, Html.InputValue(response.Body, "setupCode"));
        Assert.Equal(string.Empty, Html.InputValue(response.Body, "password"));
        Assert.Equal(string.Empty, Html.InputValue(response.Body, "passwordConfirmation"));
    }

    [Fact]
    public async Task ValidationFailure_CreatesNoOwnerAndNoAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var response = await PostSetupAsync(host, TestData.SetupFields(login: "no spaces allowed"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Null(response.SetCookie(SetCookieHeader.SessionCookieName));
        Assert.Equal(0, await host.OwnerCountAsync(ct));
        Assert.Empty(await host.AuditRowsAsync(ct));
    }

    private static async Task<PageResponse> PostSetupAsync(
        ControlPlaneTestHost host,
        IEnumerable<KeyValuePair<string, string>> fields,
        CancellationToken cancellationToken)
    {
        using var client = host.CreateClient();
        await client.GetAsync("/setup", cancellationToken);
        return await client.PostFormAsync("/setup", fields, cancellationToken);
    }

    private static void AssertOutcome(ControlPlaneTestHost host, PageResponse response, string? expectedKey)
    {
        if (expectedKey is null)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.Status);
            Assert.Equal("/", response.LocationPath);
            return;
        }

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text(expectedKey, "uk"), response.Text, StringComparison.Ordinal);
    }
}
