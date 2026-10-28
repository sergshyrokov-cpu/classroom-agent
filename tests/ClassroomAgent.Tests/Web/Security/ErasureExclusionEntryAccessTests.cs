using System.Net;
using Microsoft.AspNetCore.Authorization;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureHostExtensions;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureTestData;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-065 AC-009, AC-010, AC-018, S-01, S-02, S-07, TC-5: the entry belongs to the Admin alone; a Dean is refused, an
/// anonymous visitor is sent to sign-in, a Dean on the forced temporary-password session to the password change; the POST
/// needs the antiforgery token; in read-only mode it is refused with 409 and only the refusal is audited.
/// </summary>
public sealed class ErasureExclusionEntryAccessTests(PostgreSqlFixture database)
{
    private const string ChangePasswordPath = "/sign-in/change-password";

    /// <summary>AC-010, TC-5: an Admin enters an exclusion (the allowed-role half).</summary>
    [Fact]
    public async Task AnAdmin_EntersAnExclusion()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.PostEntryAsync(TargetEmail, ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(PagePath, page.LocationPath);
        Assert.Single(await host.ExclusionRowsAsync(ct));
    }

    /// <summary>AC-010, S-01, TC-5: a Dean is refused, sees nothing of the person, and nothing changes.</summary>
    [Fact]
    public async Task ADean_IsForbidden_AndNothingChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.SeedErasureWorldAsync(ct);
        var before = await host.ErasureFingerprintAsync(ct);

        var page = await client.PostEntryAsync(TargetEmail, ct);

        Assert.Equal(HttpStatusCode.Forbidden, page.Status);
        Assert.DoesNotContain(TargetEmail, page.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
    }

    /// <summary>AC-010, S-02: a Dean on the forced temporary-password session is sent to change it and nothing changes.</summary>
    [Fact]
    public async Task ADeanWithATemporaryPassword_IsSentToTheForcedChange()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;
        await host.SeedErasureWorldAsync(ct);
        var before = await host.ErasureFingerprintAsync(ct);

        var page = await client.PostEntryAsync(TargetEmail, ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(ChangePasswordPath, page.LocationPath);
        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
    }

    /// <summary>AC-010, S-01, TC-5: an anonymous visitor is sent to sign-in and nothing is stored.</summary>
    [Fact]
    public async Task Anonymous_IsSentToSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        await client.GetAsync(SignInTestData.SignInPath, ct);

        var page = await client.PostFormAsync(EntriesPath, [new(Fields.Email, TargetEmail)], ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(SignInTestData.SignInPath, page.LocationPath);
        Assert.Empty(await host.ExclusionRowsAsync(ct));
    }

    /// <summary>TC-5: the entry route exists once, is not anonymous, and declares the erasure policy.</summary>
    [Fact]
    public async Task TheEntryPath_IsAProtectedEndpoint_WithTheErasurePolicy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services);

        var endpoint = Assert.Single(endpoints, e => e.Pattern == Patterns.Entries);
        Assert.False(endpoint.AllowsAnonymous, Patterns.Entries);
        Assert.False(endpoint.IsFallback, Patterns.Entries);
        Assert.Contains(
            endpoint.Endpoint.Metadata.OfType<IAuthorizeData>(),
            a => string.Equals(a.Policy, Policy, StringComparison.Ordinal));
    }

    /// <summary>AC-018, S-07: a POST without the antiforgery token is 400, nothing changes and nothing is audited.</summary>
    [Fact]
    public async Task WithoutAnAntiforgeryToken_TheEntryIs400_AndNothingChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedErasureWorldAsync(ct);
        var before = await host.ErasureFingerprintAsync(ct);

        var page = await client.PostEntryAsync(TargetEmail, ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
        Assert.Empty(await host.ErasureAuditRowsAsync(ct));
    }

    /// <summary>AC-009: in read-only mode the entry is 409, nothing is stored, and only the refusal is audited.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheEntryIs409_NothingIsStored_AndOnlyTheRefusalIsAudited(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct, cause);
        await using var _host = host;

        var page = await client.PostEntryAsync(TargetEmail, ct);

        Assert.Equal(HttpStatusCode.Conflict, page.Status);
        Assert.Empty(await host.ExclusionRowsAsync(ct));
        Assert.Empty(await host.SaltRowsAsync(ct));
        var row = Assert.Single(await host.ErasureAuditRowsAsync(ct));
        Assert.Equal(Codes.EnteredAction, row.Action);
        Assert.Equal(Codes.Refused, row.Outcome);
        Assert.Equal(Codes.ReadOnlyCategory, row.RefusalCategory);
        Assert.Null(row.TargetType);
        Assert.Null(row.TargetId);
    }

    /// <summary>AC-009: in read-only mode an invalid email is still 409 (the guard comes first).</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_AnInvalidEmail_IsStill409(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct, cause);
        await using var _host = host;

        var page = await client.PostEntryAsync("not-an-email", ct);

        Assert.Equal(HttpStatusCode.Conflict, page.Status);
    }

    /// <summary>API-4: a GET to the entry address stores nothing and is neither a redirect nor a success.</summary>
    [Fact]
    public async Task AGetToTheEntryAddress_StoresNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.GetAsync(EntriesPath, ct);

        Assert.True((int)page.Status >= 400, $"A GET to the entry address answered {(int)page.Status}.");
        Assert.Empty(await host.ExclusionRowsAsync(ct));
    }
}
