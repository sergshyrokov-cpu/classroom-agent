using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-012: the first-run setup audit rows and what is deliberately not audited (FR-012, OD-004).</summary>
public sealed class SetupAuditTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task ValidSetup_WritesSucceededRowWithNewOwnerAsActorAndTarget()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        using var client = await host.CreateOwnerAsync(ct);

        var owner = await host.OwnerAsync(ct);
        var row = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal("owner", row.ActorType);
        Assert.Equal(owner!.Id, row.ActorId);
        Assert.Equal("owner_first_run_setup", row.Action);
        Assert.Equal("owner", row.TargetType);
        Assert.Equal(owner.Id, row.TargetId);
        Assert.Equal("succeeded", row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.False(string.IsNullOrWhiteSpace(row.RequestId));
        Assert.Equal(host.Time.GetUtcNow(), row.OccurredAt);
        Assert.Equal(row.CreatedAt, row.UpdatedAt);
    }

    [Fact]
    public async Task WrongCode_WritesRefusedAnonymousWrongSetupCode()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var response = await PostSetupAsync(host, TestData.SetupFields(setupCode: TestData.OtherSetupCode), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        AssertWrongSetupCodeRow(host, Assert.Single(await host.AuditRowsAsync(ct)));
    }

    [Fact]
    public async Task MissingCodeWithValidFields_IsAuditedAsWrongSetupCode()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var response = await PostSetupAsync(host, TestData.SetupFields(setupCode: null), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        AssertWrongSetupCodeRow(host, Assert.Single(await host.AuditRowsAsync(ct)));
    }

    [Fact]
    public async Task WrongCodeWithInvalidFields_IsNotAudited()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var response = await PostSetupAsync(
            host,
            TestData.SetupFields(setupCode: TestData.OtherSetupCode, login: "ab"),
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text("Setup.Login.Length", "uk"), response.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text("Setup.SetupCode.Invalid", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Empty(await host.AuditRowsAsync(ct));
    }

    [Fact]
    public async Task WrongCodeAfterOwnerExists_IsNotAudited()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var stale = host.CreateClient();
        await stale.GetAsync("/setup", ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var response = await stale.PostFormAsync(
            "/setup",
            TestData.SetupFields(setupCode: TestData.OtherSetupCode, login: "second.owner"),
            ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        var row = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal("succeeded", row.Outcome);
    }

    [Fact]
    public async Task SetupAuditRows_ContainNoCodeLoginOrPassword()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();
        await client.GetAsync("/setup", ct);

        await client.PostFormAsync("/setup", TestData.SetupFields(setupCode: TestData.OtherSetupCode), ct);
        await client.PostFormAsync("/setup", TestData.SetupFields(), ct);

        var rows = await host.AuditRowsAsJsonAsync(ct);
        Assert.Equal(2, rows.Count);
        var typedValues = new[]
        {
            TestData.SetupCode,
            TestData.SetupCode.Replace("-", string.Empty, StringComparison.Ordinal),
            TestData.OtherSetupCode,
            TestData.OtherSetupCode.Replace("-", string.Empty, StringComparison.Ordinal),
            TestData.Login,
            TestData.Password,
        };
        foreach (var typed in typedValues)
        {
            Assert.All(rows, row => Assert.DoesNotContain(typed, row, StringComparison.OrdinalIgnoreCase));
        }
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

    private static void AssertWrongSetupCodeRow(ControlPlaneTestHost host, AuditRow row)
    {
        Assert.Equal("anonymous", row.ActorType);
        Assert.Null(row.ActorId);
        Assert.Equal("owner_first_run_setup", row.Action);
        Assert.Null(row.TargetType);
        Assert.Null(row.TargetId);
        Assert.Equal("refused", row.Outcome);
        Assert.Equal("wrong_setup_code", row.RefusalCategory);
        Assert.False(string.IsNullOrWhiteSpace(row.RequestId));
        Assert.Equal(host.Time.GetUtcNow(), row.OccurredAt);
        Assert.Equal(row.CreatedAt, row.UpdatedAt);
    }
}
