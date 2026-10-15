using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureHostExtensions;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureTestData;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-038 AC-008, AC-017, spec FR-014, I-8, I-12, TC-5: in read-only mode (each BR-025 cause) the page, the search and the
/// confirmation still work with a notice, while the erasure and the lifting are refused with 409 and only the refusal is
/// audited.
/// </summary>
public sealed class ErasureReadOnlyHttpTests(PostgreSqlFixture database)
{
    /// <summary>AC-017, I-8: the page and the search still work, with the read-only notice.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task ThePageAndTheSearch_StillWork_WithTheNotice(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct, cause);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);

        var page = await client.GetAsync(PagePath, ct);
        var search = await client.PostSearchAsync("Коваль", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(HasElement(page.Body, Markup.ReadOnlyNotice));
        Assert.Equal(HttpStatusCode.OK, search.Status);
        Assert.Contains(world.Target, ResultIds(search.Body));
    }

    /// <summary>AC-017, I-8: the confirmation still opens, with the notice and a disabled button.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task TheConfirmation_StillOpens_WithTheNotice_AndADisabledButton(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct, cause);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);

        var page = await client.GetAsync(ConfirmationPath(world.Target), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(HasElement(page.Body, Markup.ReadOnlyNotice));
        Assert.Contains("disabled", Inner(page.Body, Markup.EraseForm), StringComparison.Ordinal);
    }

    /// <summary>AC-008, AC-017, FR-014, I-12: an erasure is 409, nothing is erased, and only the refusal is audited.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task AnErasure_Is409_AndOnlyTheRefusalIsAudited(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct, cause);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);
        var before = await host.ErasureFingerprintAsync(ct);

        var page = await client.PostErasureAsync(world.Target, "true", ct);

        Assert.Equal(HttpStatusCode.Conflict, page.Status);
        Assert.Equal(
            1L,
            await host.ScalarAsync<long>(
                "SELECT count(*) FROM classroom_participant WHERE id = @id", ct, ("id", world.Target)));
        var row = Assert.Single(await host.ErasureAuditRowsAsync(ct));
        Assert.Equal(Codes.ErasedAction, row.Action);
        Assert.Equal(Codes.Refused, row.Outcome);
        Assert.Equal(Codes.ReadOnlyCategory, row.RefusalCategory);
        Assert.Equal(world.Target, row.TargetId);
        var after = await host.ErasureFingerprintAsync(ct);
        Assert.Equal(
            before.Split(';').Where(p => !p.StartsWith("audit=", StringComparison.Ordinal)),
            after.Split(';').Where(p => !p.StartsWith("audit=", StringComparison.Ordinal)));
    }

    /// <summary>FR-014: an erasure without the mark is still 409 in read-only mode (the guard comes first).</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task AnErasureWithoutTheMark_IsStill409(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct, cause);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);

        var page = await client.PostErasureAsync(world.Target, null, ct);

        Assert.Equal(HttpStatusCode.Conflict, page.Status);
    }

    /// <summary>AC-008, FR-014: a lifting is 409, the exclusion stays, and only the refusal is audited.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task ALifting_Is409_AndTheExclusionStays(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct, cause);
        await using var _host = host;
        await host.SeedExclusionAsync(OtherGoogleUserId, OtherEmail, ct);

        var page = await client.PostLiftingAsync(OtherEmail, ct);

        Assert.Equal(HttpStatusCode.Conflict, page.Status);
        Assert.Single(await host.ExclusionRowsAsync(ct));
        var row = Assert.Single(await host.ErasureAuditRowsAsync(ct));
        Assert.Equal(Codes.LiftedAction, row.Action);
        Assert.Equal(Codes.Refused, row.Outcome);
        Assert.Null(row.TargetId);
    }

    /// <summary>AC-017: outside read-only mode the page carries no notice (and is the real page, so the check is not vacuous).</summary>
    [Fact]
    public async Task OutsideReadOnlyMode_ThereIsNoNotice()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct, ReadOnlyModeHost.Cause.NotReadOnly);
        await using var _host = host;

        var page = await client.GetAsync(PagePath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(HasElement(page.Body, Markup.SearchForm));
        Assert.False(HasElement(page.Body, Markup.ReadOnlyNotice));
    }
}
