using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Ls = ClassroomAgent.Tests.TestInfrastructure.ListSearchTestData;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;
using S = ClassroomAgent.Tests.TestInfrastructure.SearchTestData;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-074 AC-007, TC-5: searching a Meet code by its organizer's name keeps the authorization of both searches (Admin and Dean
/// allowed, an anonymous visitor sent to sign-in, a Dean on the forced temporary-password session sent to the password change),
/// works in read-only mode without a Google call, and leaves the name out of every audit row and log line.
/// </summary>
public sealed class MeetCodeOrganizerNameAccessTests(PostgreSqlFixture database)
{
    private const string ChangePasswordPath = "/sign-in/change-password";

    private const string Code = "zzo-aaaa-bbb";

    private const string Word = "zzorgname";

    private const string FullName = "Zzorgname Іван";

    private static readonly DateTimeOffset At = new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    public static TheoryData<Actor> Allowed => new(Actor.Admin, Actor.Dean);

    private static async Task SeedAsync(InstallationTestHost host, CancellationToken ct)
    {
        await host.PrepareAsync(ct);
        var organizer = Mc.Email("zz.organizer");
        await CourseRows.InsertParticipantAsync(host, ct, googleUserId: "7400000000001", email: organizer, fullName: FullName);
        await host.SeedMeetingAsync(Code, organizer, At, ct);
    }

    /// <summary>AC-007: an Admin and a Dean find the code by the name on the page search and on the header search.</summary>
    [Theory]
    [MemberData(nameof(Allowed))]
    public async Task AnAdminAndADean_FindTheCode_ByTheOrganizersName(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var list = await client.GetAsync(Ls.MeetUrl(q: Word), ct);
        var header = await client.GetAsync(S.Url(Word), ct);

        Assert.Equal(HttpStatusCode.OK, list.Status);
        Assert.Equal([Code], Ls.ListedCodes(list.Body));
        Assert.Equal(HttpStatusCode.OK, header.Status);
        Assert.Contains(Code, S.TextOf(S.RequireGroup(header.Body, S.MeetCodes)), StringComparison.Ordinal);
    }

    /// <summary>AC-007, TC-5: an anonymous visitor is sent to sign-in on both searches.</summary>
    [Fact]
    public async Task Anonymous_IsSentToSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        foreach (var url in new[] { Ls.MeetUrl(q: Word), S.Url(Word) })
        {
            var page = await client.GetAsync(url, ct);

            Assert.Equal(HttpStatusCode.Redirect, page.Status);
            Assert.StartsWith(SignInTestData.SignInPath, page.LocationPath, StringComparison.Ordinal);
            Assert.DoesNotContain(Code, page.Body, StringComparison.Ordinal);
        }
    }

    /// <summary>AC-007, TC-5: a Dean on the forced temporary-password session is sent to the password change on both searches.</summary>
    [Fact]
    public async Task ARestrictedDean_IsSentToTheForcedChange()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        foreach (var url in new[] { Ls.MeetUrl(q: Word), S.Url(Word) })
        {
            var page = await client.GetAsync(url, ct);

            Assert.Equal(HttpStatusCode.Redirect, page.Status);
            Assert.Equal(ChangePasswordPath, page.LocationPath);
            Assert.DoesNotContain(Code, page.Body, StringComparison.Ordinal);
        }
    }

    /// <summary>AC-007, BR-026: in read-only mode both searches work, the cell shows the name, and no Google call is made.</summary>
    [Fact]
    public async Task InReadOnlyMode_TheSearchWorks_AndCallsNoGoogleApi()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct, ReadOnlyModeHost.Cause.Suspended);
        await using var _host = host;
        await SeedAsync(host, ct);

        var list = await client.GetAsync(Ls.MeetUrl(q: Word), ct);
        var header = await client.GetAsync(S.Url(Word), ct);

        Assert.Equal(HttpStatusCode.OK, list.Status);
        Assert.Equal([Code], Ls.ListedCodes(list.Body));
        Assert.Contains(FullName, Ls.CellsOfCode(list.Body, Code)[0], StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, header.Status);
        Assert.Contains(Code, S.TextOf(S.RequireGroup(header.Body, S.MeetCodes)), StringComparison.Ordinal);
        Assert.Equal(0, host.GoogleCallCount());
    }

    /// <summary>AC-007, SC-10: the name searched is in no audit row and no log line; the controls: both searches found the code.</summary>
    [Fact]
    public async Task TheSearchedName_IsInNoAuditRow_AndNoLogLine()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var list = await client.GetAsync(Ls.MeetUrl(q: Word), ct);
        var header = await client.GetAsync(S.Url(Word), ct);
        Assert.Equal([Code], Ls.ListedCodes(list.Body));
        Assert.Contains(Code, S.TextOf(S.RequireGroup(header.Body, S.MeetCodes)), StringComparison.Ordinal);

        var audit = await host.AuditRowsAsJsonAsync(ct);
        Assert.DoesNotContain(audit, row => row.Contains(Word, StringComparison.OrdinalIgnoreCase));
        var events = await host.ReadLogEventsAsync(ct);
        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.DoesNotContain(Word, e.Line, StringComparison.OrdinalIgnoreCase));
    }
}
