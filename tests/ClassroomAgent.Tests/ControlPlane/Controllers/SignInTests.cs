using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-005: Owner sign-in over HTTP (FR-007, FR-008, VR-005).</summary>
public sealed class SignInTests(PostgreSqlFixture database)
{
    private const string WrongPassword = "this is not the password";

    [Fact]
    public async Task CorrectCredentials_SignInAndRedirectHome()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var setup = await host.CreateOwnerAsync(ct);

        var (client, response) = await host.SignInAsync(TestData.Login, TestData.Password, ct);
        using var _ = client;
        var home = await client.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/", response.LocationPath);
        Assert.NotNull(response.SetCookie(SetCookieHeader.SessionCookieName));
        Assert.Equal(HttpStatusCode.OK, home.Status);
    }

    [Fact]
    public async Task LoginInDifferentCase_SignsIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var setup = await host.CreateOwnerAsync(ct);

        var (client, response) = await host.SignInAsync("OWNER.One", TestData.Password, ct);
        using var _ = client;

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/", response.LocationPath);
    }

    [Fact]
    public async Task ReturnUrl_IsIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var setup = await host.CreateOwnerAsync(ct);
        using var client = host.CreateClient();
        await client.GetAsync("/sign-in", ct);

        var response = await client.PostFormAsync(
            "/sign-in?ReturnUrl=https%3A%2F%2Fevil.example%2F",
            TestData.SignInFields().Append(new("ReturnUrl", "https://evil.example/")),
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/", response.LocationPath);
    }

    [Fact]
    public async Task UnknownLoginWrongPasswordAndLockout_ProduceIdenticalResponses()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var setup = await host.CreateOwnerAsync(ct);
        using var client = host.CreateClient();
        await client.GetAsync("/sign-in", ct);
        const string unknownLogin = "owner.two";

        var unknown = await client.PostFormAsync("/sign-in", TestData.SignInFields(unknownLogin, TestData.Password), ct);
        var wrongPassword = await client.PostFormAsync("/sign-in", TestData.SignInFields(password: WrongPassword), ct);
        for (var attempt = 2; attempt <= 5; attempt++)
        {
            await client.PostFormAsync("/sign-in", TestData.SignInFields(password: WrongPassword), ct);
        }

        var lockedOut = await client.PostFormAsync("/sign-in", TestData.SignInFields(), ct);

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, lockedOut.Status);
        Assert.Contains(host.Text("SignIn.Refused", "uk"), unknown.Text, StringComparison.Ordinal);
        var expectedBody = Normalize(unknown.Body, unknownLogin);
        Assert.Equal(expectedBody, Normalize(wrongPassword.Body, TestData.Login));
        Assert.Equal(expectedBody, Normalize(lockedOut.Body, TestData.Login));
        var expectedHeaders = ComparableHeaders(unknown);
        Assert.Equal(expectedHeaders, ComparableHeaders(wrongPassword));
        Assert.Equal(expectedHeaders, ComparableHeaders(lockedOut));
    }

    [Fact]
    public async Task WrongPassword_ShowsCommonRefusalMessage_LoginRefilledPasswordEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var setup = await host.CreateOwnerAsync(ct);

        var (client, response) = await host.SignInAsync(TestData.Login, WrongPassword, ct);
        using var _ = client;

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        Assert.Contains(host.Text("SignIn.Refused", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(TestData.Login, Html.InputValue(response.Body, "login"));
        Assert.Equal(string.Empty, Html.InputValue(response.Body, "password"));
        Assert.DoesNotContain(WrongPassword, response.Text, StringComparison.Ordinal);
        Assert.Null(response.SetCookie(SetCookieHeader.SessionCookieName));
    }

    [Theory]
    [InlineData("", TestData.Password, "SignIn.Login.Required")]
    [InlineData(TestData.Login, "", "SignIn.Password.Required")]
    public async Task EmptyLoginOrPassword_Returns400_NoAuditNoCounterChange(string login, string password, string expectedKey)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var setup = await host.CreateOwnerAsync(ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var (client, response) = await host.SignInAsync(login, password, ct);
        using var _ = client;

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text(expectedKey, "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
        Assert.Equal(0, (await host.OwnerAsync(ct))?.AccessFailedCount);
    }

    private static string Normalize(string body, string typedLogin) =>
        Html.WithoutProtectedTokens(body).Replace(typedLogin, "{login}", StringComparison.OrdinalIgnoreCase);

    private static List<string> ComparableHeaders(PageResponse response) =>
        response.Headers
            .Where(h => !h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)
                || !h.Value.StartsWith(SetCookieHeader.AntiforgeryCookieName + "=", StringComparison.Ordinal))
            .Where(h => !h.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            .Select(h => $"{h.Key.ToLowerInvariant()}: {h.Value}")
            .Order(StringComparer.Ordinal)
            .ToList();
}
