using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Services;

/// <summary>AC-007: the one-time setup code at startup — console only, never the log file (FR-002, DC-10).</summary>
public sealed class SetupCodeStartupTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task StartupWithoutOwner_PrintsCodeToOperatorConsole()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        Assert.Equal(1, host.CodeGenerator.Calls);
        Assert.Contains(host.OperatorConsole.Lines, line => line.Contains(host.CodeGenerator.Code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartupWithoutOwner_LogFileDoesNotContainCode()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        const string wrongPassword = "this is not the password";
        using (var client = host.CreateClient())
        {
            await client.GetAsync("/setup", ct);
            await client.PostFormAsync("/setup", TestData.SetupFields(setupCode: TestData.OtherSetupCode), ct);
            await client.PostFormAsync("/setup", TestData.SetupFields(), ct);
            await client.PostFormAsync("/sign-out", [], ct);
            await client.GetAsync("/sign-in", ct);
            await client.PostFormAsync("/sign-in", TestData.SignInFields(password: wrongPassword), ct);
        }

        var logs = await host.ReadLogFilesAsync(ct);

        Assert.Contains(logs, content => content.Length > 0);
        var secrets = new[]
        {
            host.CodeGenerator.Code,
            host.CodeGenerator.Code.Replace("-", string.Empty, StringComparison.Ordinal),
            TestData.OtherSetupCode,
            TestData.Login,
            TestData.Password,
            wrongPassword,
        };
        foreach (var secret in secrets)
        {
            Assert.All(logs, content => Assert.DoesNotContain(secret, content, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task StartupWithOwner_GeneratesAndPrintsNoCode()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var first = await ControlPlaneTestHost.StartAsync(database, ct);
        using (await first.CreateOwnerAsync(ct))
        {
        }

        await first.StopAsync();
        await using var restarted = ControlPlaneTestHost.Restart(first, TestData.OtherSetupCode);

        Assert.Equal(0, restarted.CodeGenerator.Calls);
        Assert.Empty(restarted.OperatorConsole.Lines);
    }

    [Fact]
    public async Task RestartWithoutOwner_NewCodeWorks_PreviousCodeRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var first = await ControlPlaneTestHost.StartAsync(database, ct);
        var previousCode = first.CodeGenerator.Code;
        await first.StopAsync();
        await using var restarted = ControlPlaneTestHost.Restart(first, TestData.OtherSetupCode);
        using var client = restarted.CreateClient();
        await client.GetAsync("/setup", ct);

        var withPreviousCode = await client.PostFormAsync("/setup", TestData.SetupFields(setupCode: previousCode), ct);
        var withNewCode = await client.PostFormAsync("/setup", TestData.SetupFields(setupCode: restarted.CodeGenerator.Code), ct);

        Assert.Equal(1, restarted.CodeGenerator.Calls);
        Assert.Equal(HttpStatusCode.BadRequest, withPreviousCode.Status);
        Assert.Contains(restarted.Text("Setup.SetupCode.Invalid", "uk"), withPreviousCode.Text, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, withNewCode.Status);
        Assert.Equal("/", withNewCode.LocationPath);
    }
}
