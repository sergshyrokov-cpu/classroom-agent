using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-010 AC-008 at the level TEST_WRITING could not reach — the row the ac-test-matrix marks DEFERRED.
/// </summary>
/// <remarks>
/// DB_DESIGN §2.1 proved that a stored row whose domain or client ID is **empty** cannot exist in PostgreSQL:
/// <c>ck_legitimacy_state_domain_length</c> requires at least three characters,
/// <c>ck_legitimacy_state_client_id_format</c> requires 10–32 digits, and neither column is nullable. So the
/// combination <c>InstallationNotConfirmed</c> with the installation **not** in read-only mode is unreachable
/// over HTTP and against the real database, and is proven here with the repository substituted (TC-1).
///
/// The branch must exist although the database prevents reaching it: the guard is against a hand-edited
/// database or a later migration that relaxes a constraint, and a constraint is a last line of defence, never
/// the control (db-design §2.1). TEST_WRITING could not write this class because it must name
/// <see cref="GetConnectionInstructionQuery"/>, which did not exist yet.
/// </remarks>
public sealed class ConnectionInstructionBranchTests
{
    private const string Domain = "school-one.example.test";

    private const string ClientId = "100000000000000000001";

    /// <summary>AC-008: an empty stored client ID is not a known client ID — the unreachable-in-SQL branch.</summary>
    [Fact]
    public async Task AnEmptyStoredClientId_IsNotConfirmed()
    {
        var ct = TestContext.Current.CancellationToken;
        var query = QueryOver(Confirmed(Domain, clientId: string.Empty));

        var view = await query.ExecuteAsync(ct);

        Assert.Equal(ConnectionInstructionState.InstallationNotConfirmed, view.State);
    }

    /// <summary>AC-008: nor is an empty stored domain.</summary>
    [Fact]
    public async Task AnEmptyStoredDomain_IsNotConfirmed()
    {
        var ct = TestContext.Current.CancellationToken;
        var query = QueryOver(Confirmed(domain: string.Empty, clientId: ClientId));

        var view = await query.ExecuteAsync(ct);

        Assert.Equal(ConnectionInstructionState.InstallationNotConfirmed, view.State);
    }

    /// <summary>
    /// AC-008: "the page does not fall over" — the DTO carries <c>null</c>, never an empty string, so the view's
    /// fallback to the not-yet-confirmed statement engages instead of rendering a blank value.
    /// </summary>
    [Fact]
    public async Task AnEmptyStoredValue_IsReportedAsNullAndNotAsAnEmptyString()
    {
        var ct = TestContext.Current.CancellationToken;
        var query = QueryOver(Confirmed(domain: string.Empty, clientId: string.Empty));

        var view = await query.ExecuteAsync(ct);

        Assert.Null(view.InstallationDomain);
        Assert.Null(view.ServiceAccountClientId);
    }

    /// <summary>
    /// AC-008: the school-independent part of the instruction is complete even then — the scope list comes from
    /// the constant in <c>Domain</c>, not from the Control Plane (spec FR-004).
    /// </summary>
    [Fact]
    public async Task WithNothingConfirmed_TheScopeListIsStillComplete()
    {
        var ct = TestContext.Current.CancellationToken;
        var query = QueryOver(state: null);

        var view = await query.ExecuteAsync(ct);

        Assert.Equal(ConnectionInstructionState.InstallationNotConfirmed, view.State);
        Assert.Equal(GoogleDelegationScopes.All, view.Scopes);
    }

    /// <summary>
    /// AC-005, spec I-1: a domain and client ID recorded by an <c>upgrade_required</c> answer license nothing —
    /// the same rule the connection settings apply.
    /// </summary>
    [Fact]
    public async Task AnUpgradeRequiredAnswer_IsNotAConfirmedInstallation()
    {
        var ct = TestContext.Current.CancellationToken;
        var query = QueryOver(LegitimacyState.FromUpgradeRequired(InstallationStatus.Active, Domain, ClientId));

        var view = await query.ExecuteAsync(ct);

        Assert.Equal(ConnectionInstructionState.InstallationNotConfirmed, view.State);
        Assert.Null(view.ServiceAccountClientId);
    }

    /// <summary>AC-002: with both values confirmed the same call reports them, so the branches above are the only difference.</summary>
    [Fact]
    public async Task WithBothValuesConfirmed_TheInstructionIsComplete()
    {
        var ct = TestContext.Current.CancellationToken;
        var query = QueryOver(Confirmed(Domain, ClientId));

        var view = await query.ExecuteAsync(ct);

        Assert.Equal(ConnectionInstructionState.Complete, view.State);
        Assert.Equal(Domain, view.InstallationDomain);
        Assert.Equal(ClientId, view.ServiceAccountClientId);
    }

    /// <summary>AC-007: the query stages nothing — the substituted repository fails the test if it is asked to.</summary>
    [Fact]
    public async Task TheQuery_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var states = new StateRepository(Confirmed(Domain, ClientId));
        var query = new GetConnectionInstructionQuery(states);

        await query.ExecuteAsync(ct);

        Assert.Equal(1, states.ReadsForRead);
        Assert.Equal(0, states.ReadsForUpdate);
    }

    private static GetConnectionInstructionQuery QueryOver(LegitimacyState? state) =>
        new(new StateRepository(state));

    private static LegitimacyState Confirmed(string domain, string clientId) =>
        LegitimacyState.FromSuccess(
            InstallationTestHost.DefaultStart,
            InstallationStatus.Active,
            CompatibilityState.Supported,
            domain,
            clientId);

    /// <summary>
    /// The one port the query has. <c>Add</c> and the tracked read fail the test: the instruction is a read, and
    /// a tracked entity could be written by an unrelated commit in the same scope (spec FR-010).
    /// </summary>
    private sealed class StateRepository(LegitimacyState? state) : ILegitimacyStateRepository
    {
        public int ReadsForRead { get; private set; }

        public int ReadsForUpdate { get; private set; }

        public Task<LegitimacyState?> GetAsync(CancellationToken cancellationToken)
        {
            ReadsForUpdate++;
            return Task.FromResult(state);
        }

        public Task<LegitimacyState?> GetForReadAsync(CancellationToken cancellationToken)
        {
            ReadsForRead++;
            return Task.FromResult(state);
        }

        public void Add(LegitimacyState newState) =>
            throw new NotSupportedException("The instruction writes nothing (US-010 spec FR-010).");
    }
}
