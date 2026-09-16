using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-001, AC-003: the setup page before and after the Owner account exists (FR-003).</summary>
public sealed class SetupPageTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task SetupPage_WithoutOwner_ShowsCodeLoginPasswordAndConfirmation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.GetAsync("/setup", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.True(Html.HasInput(response.Body, "setupCode"));
        Assert.True(Html.HasInput(response.Body, "login"));
        Assert.True(Html.HasInput(response.Body, "password"));
        Assert.True(Html.HasInput(response.Body, "passwordConfirmation"));
        Assert.False(string.IsNullOrEmpty(Html.InputValue(response.Body, Html.AntiforgeryFieldName)));
        Assert.Equal(string.Empty, Html.InputValue(response.Body, "setupCode"));
        Assert.Equal(string.Empty, Html.InputValue(response.Body, "password"));
        Assert.Equal(string.Empty, Html.InputValue(response.Body, "passwordConfirmation"));
        Assert.DoesNotContain(host.CodeGenerator.Code, response.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetupPage_WithOwner_Anonymous_RedirectsToSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        using var anonymous = host.CreateClient();

        var response = await anonymous.GetAsync("/setup", ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/sign-in", response.LocationPath);
        Assert.DoesNotContain(TestData.Login, response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(response.Headers, h => h.Value.Contains(TestData.Login, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SetupPage_WithOwner_SignedIn_RedirectsHome()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await owner.GetAsync("/setup", ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/", response.LocationPath);
    }
}
