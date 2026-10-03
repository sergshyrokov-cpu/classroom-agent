using System.Net;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-009 AC-009: saving the connection is one of the administrative changes read-only mode blocks
/// (BR-026, which names connection settings explicitly), while viewing keeps working. The refusal is
/// enforced in <c>Application</c> and reaches HTTP as the `409` US-008 mapped; the BR-026 closed list of
/// permitted service writes is **not** widened (spec FR-008, I-8; AD-6; TC-5).
/// </summary>
public sealed class WorkspaceConnectionReadOnlyTests(PostgreSqlFixture database)
{
    /// <summary>AC-009: in every read-only cause the save is refused with `409`.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheSaveIsRefused(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;

        var response = await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
    }

    /// <summary>AC-009: nothing is written to the connection table.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_NothingIsWrittenToTheConnection(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        Assert.Empty(await host.WorkspaceConnectionsAsync(ct));
    }

    /// <summary>AC-009: a connection stored before the installation became read-only is left alone.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_AStoredConnectionIsUntouched(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;
        await host.InsertWorkspaceConnectionAsync(ct, impersonationUserEmail: WorkspaceConnectionTestData.TechnicalAccount);
        var before = await host.WorkspaceConnectionsAsync(ct);

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.OtherTechnicalAccount, ct);

        Assert.Equal(before, await host.WorkspaceConnectionsAsync(ct));
    }

    /// <summary>AC-009, spec I-8: the refusal is audited — an audit row is on the BR-026 closed list.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheRefusalIsAudited(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;

        await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        var row = Assert.Single(await host.ConnectionAuditRowsAsync(ct));
        Assert.Equal("refused", row.Outcome);
        Assert.Contains(
            row.RefusalCategory,
            new[] { WorkspaceConnectionTestData.Audit.ReadOnlyMode, WorkspaceConnectionTestData.Audit.DomainNotConfirmed });
    }

    /// <summary>AC-009: the read-only refusal names its reason, in the user's language (BR-025).</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyReasons))]
    public async Task TheRefusal_NamesTheReason(ReadOnlyModeHost.Cause cause, string reasonKey)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;

        var response = await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        Assert.Contains(host.Text(reasonKey, "uk"), response.Text, StringComparison.Ordinal);
    }

    public static TheoryData<ReadOnlyModeHost.Cause, string> ReadOnlyReasons => new()
    {
        { ReadOnlyModeHost.Cause.Suspended, SignInTestData.TextKeys.RefusedSuspendedByOwner },
        { ReadOnlyModeHost.Cause.GracePeriodExpired, SignInTestData.TextKeys.RefusedGracePeriodExpired },
        { ReadOnlyModeHost.Cause.NeverConfirmed, SignInTestData.TextKeys.RefusedNotYetConfirmed },
    };

    /// <summary>
    /// AC-009: the BR-026 closed list is unchanged. Saving a connection is not a service write, and the
    /// enum keeps exactly the four members US-007 established — widening it requires
    /// <c>trebovaniya.md</c> §2 to change first.
    /// </summary>
    [Fact]
    public void ThePermittedServiceWriteList_IsUnchanged()
    {
        Assert.Equal(
            new[]
            {
                PermittedServiceWrite.AuditEvent,
                PermittedServiceWrite.SignInBookkeeping,
                PermittedServiceWrite.LegitimacyCheckState,
                PermittedServiceWrite.RetentionPurge,
            },
            Enum.GetValues<PermittedServiceWrite>());
    }

    /// <summary>
    /// AC-009: the connection save is guarded, not exempt — no use case of US-009 is registered as a permitted
    /// service write.
    /// </summary>
    /// <remarks>
    /// The registry grew from three entries to six in US-012, which declares the Dean's sign-in and the two
    /// password changes as <c>SignInBookkeeping</c> — the BR-026 member that already names sign-in bookkeeping
    /// and "a Dean changing their own password". The closed list itself did not grow, which the test above
    /// asserts; this test keeps its own point, that nothing of US-009 is exempt. US-039 (spec FR-007) added the
    /// seventh: a user choosing their UI language, under the same member.
    /// </remarks>
    [Fact]
    public void NoNewUseCase_IsRegisteredAsAPermittedServiceWrite()
    {
        // Eight since US-037 registered the retention purge (BR-075); none of them concerns the connection.
        Assert.Equal(8, PermittedServiceWrites.Declarations.Count);
        Assert.DoesNotContain(
            PermittedServiceWrites.Declarations.Keys,
            t => t.Name.Contains("WorkspaceConnection", StringComparison.Ordinal));
    }

    /// <summary>AC-009, BR-026: viewing works in read-only mode, so the page is served while the save is not.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_ViewingWorksWhileSavingDoesNot(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, ct, cause);
        await using var _host = host;

        var page = await client.OpenSettingsAsync(ct);
        var save = await client.SaveConnectionAsync(WorkspaceConnectionTestData.TechnicalAccount, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(HttpStatusCode.Conflict, save.Status);
    }
}
