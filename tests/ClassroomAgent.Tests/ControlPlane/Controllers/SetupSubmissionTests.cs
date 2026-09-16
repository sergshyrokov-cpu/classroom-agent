using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Identity;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-002, AC-003, AC-007: submitting the setup form (FR-004, FR-005).</summary>
public sealed class SetupSubmissionTests(PostgreSqlFixture database)
{
    private const string SecondLogin = "second.owner";

    [Fact]
    public async Task ValidSetup_CreatesOwnerSignsInAndRedirectsHome()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();
        await client.GetAsync("/setup", ct);

        var response = await client.PostFormAsync("/setup", TestData.SetupFields(), ct);
        var home = await client.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/", response.LocationPath);
        Assert.NotNull(response.SetCookie(SetCookieHeader.SessionCookieName));
        Assert.Equal(HttpStatusCode.OK, home.Status);
    }

    [Fact]
    public async Task ValidSetup_StoresExactlyOneOwner()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        using var client = await host.CreateOwnerAsync(ct);

        Assert.Equal(1, await host.OwnerCountAsync(ct));
        var owner = await host.OwnerAsync(ct);
        Assert.NotNull(owner);
        Assert.Equal(TestData.Login, owner.UserName);
        Assert.Equal(TestData.Login.ToUpperInvariant(), owner.NormalizedUserName);
        Assert.Equal(0, owner.AccessFailedCount);
        Assert.Null(owner.LockoutEnd);
    }

    [Fact]
    public async Task ValidSetup_StoresPasswordOnlyAsVerifiableHash()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        using var client = await host.CreateOwnerAsync(ct);

        var owner = await host.OwnerAsync(ct);
        Assert.NotNull(owner);
        Assert.NotEqual(TestData.Password, owner.PasswordHash);
        Assert.DoesNotContain(TestData.Password, owner.PasswordHash, StringComparison.OrdinalIgnoreCase);
        var verification = new PasswordHasher<object>().VerifyHashedPassword(new object(), owner.PasswordHash, TestData.Password);
        Assert.NotEqual(PasswordVerificationResult.Failed, verification);
    }

    [Fact]
    public async Task ValidSetup_SetsUiLanguageUkrainian()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        using var client = await host.CreateOwnerAsync(ct);

        var owner = await host.OwnerAsync(ct);
        Assert.NotNull(owner);
        Assert.Equal("uk", owner.UiLanguage);
    }

    [Fact]
    public async Task Setup_WhenOwnerExists_ReturnsConflictWithSignInLink()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var stale = host.CreateClient();
        await stale.GetAsync("/setup", ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var response = await stale.PostFormAsync(
            "/setup",
            TestData.SetupFields(setupCode: "WRONG-CODE-0000", login: SecondLogin),
            ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Contains(host.Text("Setup.AlreadyCreated", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Matches("href=\"(https://localhost)?/sign-in\"", response.Body);
        Assert.DoesNotContain(TestData.Login, response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SecondLogin, response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await host.OwnerCountAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task Setup_WithPreviousCodeAfterOwnerCreated_DoesNotCreateSecondOwner()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var stale = host.CreateClient();
        await stale.GetAsync("/setup", ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await stale.PostFormAsync(
            "/setup",
            TestData.SetupFields(setupCode: host.CodeGenerator.Code, login: SecondLogin),
            ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Null(response.SetCookie(SetCookieHeader.SessionCookieName));
        Assert.Equal(1, await host.OwnerCountAsync(ct));
        Assert.Equal(TestData.Login, (await host.OwnerAsync(ct))?.UserName);
    }

    [Fact]
    public async Task WrongCode_Returns400_CreatesNothing_DoesNotRevealCode()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();
        await client.GetAsync("/setup", ct);

        var response = await client.PostFormAsync("/setup", TestData.SetupFields(setupCode: TestData.OtherSetupCode), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text("Setup.SetupCode.Invalid", "uk"), response.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.CodeGenerator.Code, response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(host.CodeGenerator.Code.Replace("-", string.Empty, StringComparison.Ordinal), response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TestData.OtherSetupCode, response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Null(response.SetCookie(SetCookieHeader.SessionCookieName));
        Assert.Equal(0, await host.OwnerCountAsync(ct));
    }

    [Fact]
    public async Task MissingCode_Returns400_TreatedAsWrongCode()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();
        await client.GetAsync("/setup", ct);

        var empty = await client.PostFormAsync("/setup", TestData.SetupFields(setupCode: null), ct);
        var absent = await client.PostFormAsync(
            "/setup",
            TestData.SetupFields().Where(f => f.Key != "setupCode"),
            ct);

        foreach (var response in new[] { empty, absent })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.Status);
            Assert.Contains(host.Text("Setup.SetupCode.Invalid", "uk"), response.Text, StringComparison.Ordinal);
        }

        Assert.Equal(0, await host.OwnerCountAsync(ct));
    }
}
