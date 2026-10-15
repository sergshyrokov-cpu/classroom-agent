using System.Net;
using System.Security.Claims;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureHostExtensions;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureTestData;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-038 AC-008, AC-015, AC-020, spec FR-015, FR-016, VR-005, S-01, S-02, S-08, TC-5: the five operations belong to the
/// Admin alone; a Dean is refused, an anonymous visitor is sent to sign-in, a Dean on the forced temporary-password session
/// to the password change; every POST needs the antiforgery token; a malformed or unknown id is 404; a GET never erases.
/// </summary>
public sealed class ErasureAuthorizationTests(PostgreSqlFixture database)
{
    private const string ChangePasswordPath = "/sign-in/change-password";

    private const long UnknownParticipantId = 987654L;

    public enum Operation
    {
        Page,
        Search,
        Confirmation,
        Erasure,
        Lifting,
    }

    public static TheoryData<Operation> AllOperations =>
        new(Operation.Page, Operation.Search, Operation.Confirmation, Operation.Erasure, Operation.Lifting);

    public static TheoryData<Operation> RestrictedSessionOperations => new(Operation.Page, Operation.Erasure);

    public static TheoryData<Operation> PostOperations => new(Operation.Search, Operation.Erasure, Operation.Lifting);

    private static ClaimsPrincipal PrincipalOf(string role) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, "1")],
            CookieAuthenticationDefaults.AuthenticationScheme));

    /// <summary>Performs the operation through the real helpers (token obtained as a signed-in user would).</summary>
    private static Task<PageResponse> PerformAsync(FormClient client, Operation operation, long target, CancellationToken cancellationToken) =>
        operation switch
        {
            Operation.Page => client.GetAsync(PagePath, cancellationToken),
            Operation.Search => client.PostSearchAsync("Коваль", cancellationToken),
            Operation.Confirmation => client.GetAsync(ConfirmationPath(target), cancellationToken),
            Operation.Erasure => client.PostErasureAsync(target, "true", cancellationToken),
            Operation.Lifting => client.PostLiftingAsync(TargetEmail, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };

    /// <summary>Performs the operation as an anonymous visitor, with a token taken from the sign-in page.</summary>
    private static async Task<PageResponse> PerformAnonymouslyAsync(
        FormClient client,
        Operation operation,
        long target,
        CancellationToken cancellationToken)
    {
        switch (operation)
        {
            case Operation.Page:
                return await client.GetAsync(PagePath, cancellationToken);
            case Operation.Confirmation:
                return await client.GetAsync(ConfirmationPath(target), cancellationToken);
        }

        // The sign-in page supplies a token, so the refusal under test is the authorization one, not the antiforgery one.
        await client.GetAsync(SignInTestData.SignInPath, cancellationToken);
        return operation switch
        {
            Operation.Search => await client.PostFormAsync(SearchesPath, [new(Fields.Term, "Коваль")], cancellationToken),
            Operation.Erasure => await client.PostFormAsync(ErasurePath(target), [new(Fields.Mark, "true")], cancellationToken),
            Operation.Lifting => await client.PostFormAsync(LiftingsPath, [new(Fields.Email, TargetEmail)], cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };
    }

    /// <summary>AC-008, FR-015: the erasure policy admits an Admin.</summary>
    [Fact]
    public async Task TheErasurePolicy_AdmitsAnAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var authorization = host.Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(PrincipalOf("Admin"), resource: null, Policy);

        Assert.True(result.Succeeded);
    }

    /// <summary>AC-008, FR-015, S-01: and refuses a Dean.</summary>
    [Fact]
    public async Task TheErasurePolicy_RefusesADean()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var authorization = host.Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(PrincipalOf("Dean"), resource: null, Policy);

        Assert.False(result.Succeeded);
    }

    /// <summary>AC-008, FR-015, TC-5: an Admin reaches the page, the search and the confirmation (the allowed-role half).</summary>
    [Fact]
    public async Task AnAdmin_ReachesThePage_TheSearch_AndTheConfirmation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);

        var page = await client.GetAsync(PagePath, ct);
        var search = await client.PostSearchAsync("Коваль", ct);
        var confirmation = await client.GetAsync(ConfirmationPath(world.Target), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(HasElement(page.Body, Markup.SearchForm));
        Assert.Equal(HttpStatusCode.OK, search.Status);
        Assert.Contains(world.Target, ResultIds(search.Body));
        Assert.Equal(HttpStatusCode.OK, confirmation.Status);
        Assert.True(HasElement(confirmation.Body, Markup.Confirmation));
    }

    /// <summary>AC-008, FR-015, S-01, TC-5: a Dean is refused every operation, sees nothing of the person, and nothing changes.</summary>
    [Theory]
    [MemberData(nameof(AllOperations))]
    public async Task ADean_IsForbidden_EveryOperation(Operation operation)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);
        var before = await host.ErasureFingerprintAsync(ct);

        var page = await PerformAsync(client, operation, world.Target, ct);

        Assert.Equal(HttpStatusCode.Forbidden, page.Status);
        Assert.DoesNotContain(TargetEmail, page.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
    }

    /// <summary>AC-008, S-01, TC-5: an anonymous visitor is sent to sign-in for every operation and nothing changes.</summary>
    [Theory]
    [MemberData(nameof(AllOperations))]
    public async Task Anonymous_IsSentToSignIn(Operation operation)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);
        var before = await host.ErasureFingerprintAsync(ct);

        var page = await PerformAnonymouslyAsync(client, operation, world.Target, ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(SignInTestData.SignInPath, page.LocationPath);
        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
    }

    /// <summary>AC-008, S-02: a Dean on the forced temporary-password session is sent to change it and nothing changes.</summary>
    [Theory]
    [MemberData(nameof(RestrictedSessionOperations))]
    public async Task ADeanWithATemporaryPassword_IsSentToTheForcedChange(Operation operation)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);
        var before = await host.ErasureFingerprintAsync(ct);

        var page = await PerformAsync(client, operation, world.Target, ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(ChangePasswordPath, page.LocationPath);
        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
    }

    /// <summary>FR-015, TC-5: each route exists once, is not anonymous, and declares the erasure policy.</summary>
    [Fact]
    public async Task TheFivePaths_AreProtectedEndpoints_WithThePolicy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var endpoints = HostEndpoint.All(host.Services);

        foreach (var pattern in Patterns.All)
        {
            var endpoint = Assert.Single(endpoints, e => e.Pattern == pattern);
            Assert.False(endpoint.AllowsAnonymous, pattern);
            Assert.False(endpoint.IsFallback, pattern);
            Assert.Contains(
                endpoint.Endpoint.Metadata.OfType<IAuthorizeData>(),
                a => string.Equals(a.Policy, Policy, StringComparison.Ordinal));
        }
    }

    /// <summary>S-08, API-7: a POST without the antiforgery token is 400, nothing changes and nothing is audited.</summary>
    [Theory]
    [MemberData(nameof(PostOperations))]
    public async Task WithoutAnAntiforgeryToken_ThePostIs400_AndNothingChanges(Operation operation)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);
        var before = await host.ErasureFingerprintAsync(ct);

        var page = operation switch
        {
            Operation.Search => await client.PostSearchAsync("Коваль", ct, withToken: false),
            Operation.Erasure => await client.PostErasureAsync(world.Target, "true", ct, withToken: false),
            Operation.Lifting => await client.PostLiftingAsync(OtherEmail, ct, withToken: false),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
        Assert.Empty(await host.ErasureAuditRowsAsync(ct));
    }

    /// <summary>VR-005: a participant id that is not a positive 64-bit number is 404.</summary>
    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("12345678901234567890")]
    public async Task AMalformedParticipantId_Is404(string rawId)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.GetAsync(ConfirmationPath(rawId), ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
    }

    /// <summary>VR-005, AC-020: an unknown participant is 404 for the confirmation and the erasure; nothing changes.</summary>
    [Fact]
    public async Task AnUnknownParticipant_Is404_ForTheConfirmationAndTheErasure_AndNothingChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);
        Assert.NotEqual(UnknownParticipantId, world.Target);
        var before = await host.ErasureFingerprintAsync(ct);

        var confirmation = await client.GetAsync(ConfirmationPath(UnknownParticipantId), ct);
        var erasure = await client.PostErasureAsync(UnknownParticipantId, "true", ct);

        Assert.Equal(HttpStatusCode.NotFound, confirmation.Status);
        Assert.Equal(HttpStatusCode.NotFound, erasure.Status);
        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
        Assert.Empty(await host.ErasureAuditRowsAsync(ct));
    }

    /// <summary>API-4: a GET to the erasure address erases nothing and is neither a redirect nor a success.</summary>
    [Fact]
    public async Task AGetToTheErasureAddress_ErasesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);
        var before = await host.ErasureFingerprintAsync(ct);

        var page = await client.GetAsync(ErasurePath(world.Target), ct);

        Assert.True((int)page.Status >= 400, $"A GET to the erasure address answered {(int)page.Status}.");
        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
    }
}
