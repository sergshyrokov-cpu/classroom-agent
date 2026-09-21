using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-011 AC-007: every run is audited without personal data (<c>trebovaniya.md</c> §5 "запуск «Проверить доступ»";
/// spec FR-008, FR-013, I-4, S-11; db-design §3.2). A carried-out run is <c>succeeded</c> whatever it found; the
/// findings are not recorded, and nothing else is written — the result is not stored (OD-003).
/// </summary>
public sealed class AccessCheckAuditTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task ACarriedOutRun_WritesOneSucceededRow_NamingTheConnection()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        var connectionId = await host.ConnectionIdAsync(ct);
        var admin = Assert.Single(await host.AppUsersAsync(ct));

        var page = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var row = Assert.Single(await host.AccessCheckAuditRowsAsync(ct));
        Assert.Equal("app_user", row.ActorType);
        Assert.Equal(admin.Id, row.ActorId);
        Assert.Equal("admin", row.ActorRole);
        Assert.Equal(AccessCheckTestData.Audit.Succeeded, row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.Equal(AccessCheckTestData.Audit.TargetType, row.TargetType);
        Assert.Equal(connectionId, row.TargetId);
        Assert.False(string.IsNullOrWhiteSpace(row.RequestId));
    }

    /// <summary>Spec I-4: "succeeded" means the check ran — a run that found problems is still one succeeded row.</summary>
    [Fact]
    public async Task ARunThatFindsProblems_IsStillAuditedAsSucceeded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        probe.Delegation[AccessCheckTestData.Scopes[2]] = AccessCheckStepOutcome.ScopeNotAuthorized;
        probe.MeetActivity = AccessCheckStepOutcome.TechnicalAccountCannotRead;

        var page = await client.RunAccessCheckAsync(ct);

        Assert.Equal(AccessCheckTestData.Verdict.NotConfigured, AccessCheckHostExtensions.VerdictOf(page));
        var row = Assert.Single(await host.AccessCheckAuditRowsAsync(ct));
        Assert.Equal(AccessCheckTestData.Audit.Succeeded, row.Outcome);
    }

    /// <summary>S-11: no row carries the technical account, the domain, a scope, a token or an outcome name.</summary>
    [Fact]
    public async Task TheAuditRows_CarryNoPersonalDataAndNoFindings()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        probe.Delegation[AccessCheckTestData.Scopes[1]] = AccessCheckStepOutcome.ScopeNotAuthorized;

        var page = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.NotEmpty(await host.AccessCheckAuditRowsAsync(ct));
        var json = string.Join('\n', await host.AuditRowsAsJsonAsync(ct));
        Assert.DoesNotContain(AccessCheckTestData.TechnicalAccount, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(InstallationTestData.Domain, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SignInTestData.AdminEmail, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("googleapis", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(FakeGoogleAccessProbe.IssuedToken, json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(AccessCheckStepOutcome.ScopeNotAuthorized), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(AccessCheckTestData.Verdict.NotConfigured, json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>OD-005: every run is a row — two runs, two rows.</summary>
    [Fact]
    public async Task EveryRun_IsItsOwnRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        await client.RunAccessCheckAsync(ct);
        await client.RunAccessCheckAsync(ct);

        Assert.Equal(2, (await host.AccessCheckAuditRowsAsync(ct)).Count);
    }

    /// <summary>OD-003, FR-013: a run writes the audit row and nothing else — every other table keeps its row count.</summary>
    [Fact]
    public async Task ARun_WritesNothingButTheAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        var before = await ConnectionInstructionHostExtensions.TableRowCountsAsync(host, ct);

        var page = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var after = await ConnectionInstructionHostExtensions.TableRowCountsAsync(host, ct);
        foreach (var (table, count) in before)
        {
            var expected = table == "audit_event" ? count + 1 : count;
            Assert.True(after[table] == expected, $"{table}: expected {expected} rows, found {after[table]}.");
        }
    }
}
