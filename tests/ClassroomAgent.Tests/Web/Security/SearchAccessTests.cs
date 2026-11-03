using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Ls = ClassroomAgent.Tests.TestInfrastructure.ListSearchTestData;
using S = ClassroomAgent.Tests.TestInfrastructure.SearchTestData;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-071 AC-007, TC-5: the results page keeps the authorization of the rest of the installation (Admin and Dean allowed, an
/// anonymous visitor sent to sign-in, a Dean on the forced temporary-password session sent to the password change), searches in
/// read-only mode, reads stored data only (no Google call, nothing written, no audit event), leaves the searched text out of every
/// log line and answers with the no-store and referrer headers (SC-10, SC-14).
/// </summary>
public sealed class SearchAccessTests(PostgreSqlFixture database)
{
    private const string ChangePasswordPath = "/sign-in/change-password";

    private static readonly string[] GroupsInTheFixedOrder = [S.Students, S.Teachers, S.Courses, S.MeetCodes];

    public static TheoryData<Actor> AllowedActors => new(Actor.Admin, Actor.Dean);

    private static string? HeaderValue(PageResponse page, string name) =>
        page.Headers.Where(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).FirstOrDefault();

    /// <summary>AC-007: an Admin and a Dean both search and get the four groups in order.</summary>
    [Theory]
    [MemberData(nameof(AllowedActors))]
    public async Task AnAdminAndADean_CanSearch(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);

        var page = await client.GetAsync(S.Url(Ls.Needle), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(GroupsInTheFixedOrder, S.GroupsInOrder(page.Body));
    }

    /// <summary>AC-007, TC-5: an anonymous visitor is sent to sign-in, and the text is neither in the address nor in the page.</summary>
    [Fact]
    public async Task Anonymous_IsSentToSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);

        var page = await client.GetAsync(S.Url(Ls.Needle), ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.StartsWith(SignInTestData.SignInPath, page.LocationPath, StringComparison.Ordinal);
        Assert.DoesNotContain(Ls.Needle, page.Location ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(Ls.Needle, page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-007, TC-5: a Dean on the forced temporary-password session is sent to the password change.</summary>
    [Fact]
    public async Task ARestrictedDean_IsSentToTheForcedChange()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);

        var page = await client.GetAsync(S.Url(Ls.Needle), ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(ChangePasswordPath, page.LocationPath);
    }

    /// <summary>AC-007, BR-026: searching only reads, so read-only mode does not stop it.</summary>
    [Fact]
    public async Task InReadOnlyMode_TheSearchStillWorks()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct, ReadOnlyModeHost.Cause.Suspended);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);

        var page = await client.GetAsync(S.Url(Ls.Needle), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(GroupsInTheFixedOrder, S.GroupsInOrder(page.Body));
    }

    /// <summary>AC-007, SC-13: a search calls no Google API, writes no row and no audit event.</summary>
    [Fact]
    public async Task ASearch_CallsNoGoogleApi_WritesNothing_AndAuditsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);
        var before = await host.CourseListFingerprintAsync(ct);
        var auditBefore = (await host.AuditRowsAsync(ct)).Count;

        var page = await client.GetAsync(S.Url(Ls.Needle), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(GroupsInTheFixedOrder, S.GroupsInOrder(page.Body));
        Assert.Equal(0, host.GoogleCallCount());
        Assert.Equal(before, await host.CourseListFingerprintAsync(ct));
        var audit = await host.AuditRowsAsJsonAsync(ct);
        Assert.DoesNotContain(audit, row => row.Contains(Ls.Needle, StringComparison.Ordinal));
        Assert.Equal(auditBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    /// <summary>
    /// AC-007, SC-10: the searched text, accepted, too short, with nothing found or refused, is in no log line. The control: the
    /// four requests really ran (three answer 200, the refused one 400) and the log is not empty.
    /// </summary>
    [Fact]
    public async Task TheSearchedText_IsInNoLogLine()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);

        var accepted = await client.GetAsync(S.Url(Ls.Needle), ct);
        var tooShort = await client.GetAsync(S.Url("z"), ct);
        var nothingFound = await client.GetAsync(S.Url(Ls.Needle + "нічого"), ct);
        var refused = await client.GetAsync(S.Path + "?q=" + Ls.Needle + "%07", ct);

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.BadRequest],
            [accepted.Status, tooShort.Status, nothingFound.Status, refused.Status]);
        var events = await host.ReadLogEventsAsync(ct);

        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.DoesNotContain(Ls.Needle, e.Line, StringComparison.Ordinal));
    }

    /// <summary>AC-007, SC-14: the page, which holds school data, is never cached.</summary>
    [Fact]
    public async Task TheResultsPage_IsNotCached()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);

        var page = await client.GetAsync(S.Url(Ls.Needle), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("no-store", HeaderValue(page, "Cache-Control"), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-007, SC-14: the page carries the referrer policy of the whole installation, so the text in the address is not passed on.</summary>
    [Fact]
    public async Task TheResultsPage_CarriesTheSameOriginReferrerPolicy()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await host.SeedOneOfEachAsync(ct);

        var page = await client.GetAsync(S.Url(Ls.Needle), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal("same-origin", HeaderValue(page, "Referrer-Policy"));
    }
}
