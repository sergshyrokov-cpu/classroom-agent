using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-009 AC-008: saving and changing the connection are audited, and so is a refused attempt — the same
/// action with the other outcome (spec FR-009, I-1; SC-11; db-design §4.2). No row carries an address, a
/// domain or any value the Admin typed.
/// </summary>
public sealed class WorkspaceConnectionAuditTests(PostgreSqlFixture database)
{
    /// <summary>AC-008: a first save writes exactly one row, naming the connection it created.</summary>
    [Fact]
    public async Task AFirstSave_WritesOneRowNamingTheConnection()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        var row = Assert.Single(await host.ConnectionAuditRowsAsync(ct));
        var connection = Assert.Single(await host.WorkspaceConnectionsAsync(ct));
        Assert.Equal("succeeded", row.Outcome);
        Assert.Equal(WorkspaceConnectionTestData.Audit.TargetType, row.TargetType);
        Assert.Equal(connection.Id, row.TargetId);
        Assert.Null(row.RefusalCategory);
    }

    /// <summary>AC-008: the actor is the Admin's account and role, never an address.</summary>
    [Fact]
    public async Task TheActor_IsTheAdminsAccountAndRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        var row = Assert.Single(await host.ConnectionAuditRowsAsync(ct));
        var admin = Assert.Single(await host.AppUsersAsync(ct));
        Assert.Equal("app_user", row.ActorType);
        Assert.Equal(admin.Id, row.ActorId);
        Assert.Equal("admin", row.ActorRole);
    }

    /// <summary>AC-008: the row carries the request id that ties it to the log line (SC-11).</summary>
    [Fact]
    public async Task TheRow_CarriesARequestId()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        var row = Assert.Single(await host.ConnectionAuditRowsAsync(ct));
        Assert.False(string.IsNullOrWhiteSpace(row.RequestId));
    }

    /// <summary>AC-008, spec I-2: a change is the same action, naming the connection that already existed.</summary>
    [Fact]
    public async Task AChange_IsAuditedAsTheSameActionOnTheSameConnection()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.OtherTechnicalAccount, ct);

        var rows = await host.ConnectionAuditRowsAsync(ct);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("succeeded", r.Outcome));
        Assert.Equal(rows[0].TargetId, rows[1].TargetId);
    }

    public static TheoryData<string, string> Refusals => new()
    {
        { WorkspaceConnectionTestData.NeighbouringSchoolAccount, WorkspaceConnectionTestData.Audit.ImpersonationDomainMismatch },
        { WorkspaceConnectionTestData.PersonalAccount, WorkspaceConnectionTestData.Audit.ImpersonationDomainMismatch },
        { WorkspaceConnectionTestData.SubdomainAccount, WorkspaceConnectionTestData.Audit.ImpersonationDomainMismatch },
    };

    /// <summary>AC-008: a refused save is audited with its category and names no row (db-design §4.1).</summary>
    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task ARefusedSave_IsAuditedWithItsCategory(string address, string category)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        await client.SaveConnectionAsync(address, ct);

        var row = Assert.Single(await host.ConnectionAuditRowsAsync(ct));
        Assert.Equal("refused", row.Outcome);
        Assert.Equal(category, row.RefusalCategory);
        Assert.Equal(WorkspaceConnectionTestData.Audit.TargetType, row.TargetType);
        Assert.Null(row.TargetId);
    }

    /// <summary>AC-008, AC-010: a save refused because no domain is known is audited too.</summary>
    [Fact]
    public async Task ASaveWithNoKnownDomain_IsAudited()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(
            database,
            ct,
            ReadOnlyModeHost.Cause.NeverConfirmed);
        await using var _host = host;

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        var row = Assert.Single(await host.ConnectionAuditRowsAsync(ct));
        Assert.Equal("refused", row.Outcome);
        Assert.Contains(
            row.RefusalCategory,
            new[] { WorkspaceConnectionTestData.Audit.DomainNotConfirmed, WorkspaceConnectionTestData.Audit.ReadOnlyMode });
    }

    /// <summary>AC-008: exactly one row per attempt, whatever the outcome.</summary>
    [Fact]
    public async Task EachAttempt_WritesExactlyOneRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);
        await client.SaveConnectionAsync(WorkspaceConnectionTestData.NeighbouringSchoolAccount, ct);
        await client.SaveConnectionAsync(WorkspaceConnectionTestData.OtherTechnicalAccount, ct);

        Assert.Equal(3, (await host.ConnectionAuditRowsAsync(ct)).Count);
    }

    /// <summary>AC-006, AC-008: a request rejected by validation never reaches the audited action.</summary>
    [Fact]
    public async Task AMalformedRequest_WritesNoAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        await client.SaveConnectionAsync("not-an-address", ct);

        Assert.Empty(await host.ConnectionAuditRowsAsync(ct));
    }

    /// <summary>AC-008, SC-11: no row carries an address, a domain or any other personal or school datum.</summary>
    [Fact]
    public async Task NoRow_CarriesTheAddressOrTheDomain()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);
        await client.SaveConnectionAsync(WorkspaceConnectionTestData.NeighbouringSchoolAccount, ct);

        var rows = await host.AuditRowsAsJsonAsync(ct);

        Assert.All(rows, row =>
        {
            Assert.DoesNotContain("@", row, StringComparison.Ordinal);
            Assert.DoesNotContain(WorkspaceConnectionTestData.AllowedDomain, row, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>AC-008, SC-11: an audit row is never updated — the immutability of US-008 still holds.</summary>
    [Fact]
    public async Task AnAuditRow_CannotBeUpdated()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);
        var row = Assert.Single(await host.ConnectionAuditRowsAsync(ct));

        var update = async () => await host.ExecuteAsync(
            "UPDATE audit_event SET updated_at = updated_at + interval '1 second' WHERE id = @id",
            ct,
            ("id", row.Id));

        await Assert.ThrowsAsync<Npgsql.PostgresException>(update);
    }
}
