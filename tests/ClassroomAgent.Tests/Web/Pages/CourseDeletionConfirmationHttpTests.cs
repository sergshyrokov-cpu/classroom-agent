using System.Globalization;
using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.DataHealthHostExtensions;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cause = ClassroomAgent.Tests.TestInfrastructure.ReadOnlyModeHost.Cause;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-062 FR-006 over HTTP and PostgreSQL (TC-2, TC-3): <c>POST /workspace/data-health/course-deletion-confirmation</c> —
/// every status of api-design §2.6: <c>302</c> on success (PRG with the one-shot message), without a session and for a
/// restricted session; <c>400</c> without the antiforgery token and for a malformed form; <c>403</c> for a Dean;
/// <c>409</c> in read-only mode and for a stale list. Nothing is written except as specified. Data is synthetic (TC-4).
/// </summary>
public sealed class CourseDeletionConfirmationHttpTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset D1 = new(2026, 9, 10, 21, 30, 0, TimeSpan.Zero);

    private static readonly DataHealthTexts Uk = DataHealthTexts.Uk;

    private sealed record World(InstallationTestHost Host, FormClient Client, long First, long Second, long Third);

    /// <summary>The data of AC-009 for the actor: three courses "not visible", the third archived.</summary>
    private static async Task<World> StartAsync(PostgreSqlFixture database, Actor actor, CancellationToken ct, Cause cause = Cause.NotReadOnly)
    {
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct, cause);
        await host.ReadyToSeedAsync(ct);
        await host.RecentSuccessAsync(ct);
        var ids = new long[3];
        for (var i = 0; i < 3; i++)
        {
            ids[i] = await host.CourseAsync(
                i + 1, $"Test Hidden {i + 1}", ct, state: i == 2 ? CourseTestData.States.Archived : CourseTestData.States.Active);
            await host.MarkCourseAsync(ids[i], "not_visible_to_technical_account", D1, ct);
        }

        return new World(host, client, ids[0], ids[1], ids[2]);
    }

    private static Task<PageResponse> PostAsync(World world, CancellationToken ct, bool withToken = true, string tokenPage = PagePath, params object[] ids) =>
        world.Client.PostWithTokenAsync(ConfirmationPath, Ids(ids), ct, withToken, tokenPage);

    /// <summary>
    /// AC-010, FR-006.2 step 6, api-design §2.6: the Admin confirms two — <c>302</c> to «Стан даних», the next page shows
    /// «Позначено як видалені в Classroom: 2» once; the marks changed and one succeeded audit row holds the two ids.
    /// </summary>
    [Fact]
    public async Task TheAdminConfirmsTwo_IsRedirected_AndSeesTheMessageOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await StartAsync(database, Actor.Admin, ct);
        await using var _host = world.Host;

        var response = await PostAsync(world, ct, ids: [world.First, world.Third]);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(PagePath, response.LocationPath);
        Assert.Equal("not_in_classroom", (await world.Host.MarkOfAsync(world.First, ct)).Mark);
        Assert.Equal("not_in_classroom", (await world.Host.MarkOfAsync(world.Third, ct)).Mark);
        Assert.Equal("not_visible_to_technical_account", (await world.Host.MarkOfAsync(world.Second, ct)).Mark);
        Assert.Equal([new[] { world.First, world.Third }.Order().ToArray()], await world.Host.ConfirmedIdsAsync(ct));

        var next = await world.Client.GetAsync(PagePath, ct);
        Assert.Equal(HttpStatusCode.OK, next.Status);
        Assert.Contains(string.Format(Uk.Culture, Uk.Confirmed, 2), Section(next.Body, MessageHook), StringComparison.Ordinal);
        Assert.Equal([world.Second.ToString(CultureInfo.InvariantCulture)], CheckboxValues(next.Body));
        var again = await world.Client.GetAsync(PagePath, ct);
        Assert.False(Has(again.Body, MessageHook));
    }

    /// <summary>AC-011, TC-5, §2 row 750: a Dean is refused <c>403</c> with the error page; no mark changes, no audit row.</summary>
    [Fact]
    public async Task ADean_IsForbidden_AndNothingChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await StartAsync(database, Actor.Dean, ct);
        await using var _host = world.Host;
        var before = await world.Host.CourseFingerprintAsync(ct);

        var response = await PostAsync(world, ct, ids: [world.First]);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Contains(world.Host.Text(SignInTestData.TextKeys.Forbidden, "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(before, await world.Host.CourseFingerprintAsync(ct));
        Assert.Empty(await world.Host.ConfirmedIdsAsync(ct));
    }

    /// <summary>
    /// AC-011, AD-6, api-design §2.4: in read-only mode the Admin is refused <c>409</c> naming the reason; no mark changes;
    /// only the refused audit row is written.
    /// </summary>
    [Theory]
    [InlineData(Cause.NeverConfirmed, SignInTestData.TextKeys.RefusedNotYetConfirmed)]
    [InlineData(Cause.Suspended, SignInTestData.TextKeys.RefusedSuspendedByOwner)]
    [InlineData(Cause.GracePeriodExpired, SignInTestData.TextKeys.RefusedGracePeriodExpired)]
    public async Task InReadOnlyMode_TheAdminIsRefusedWithTheReason(Cause cause, string reasonKey)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await StartAsync(database, Actor.Admin, ct, cause);
        await using var _host = world.Host;
        var before = await world.Host.CourseFingerprintAsync(ct);

        var response = await PostAsync(world, ct, ids: [world.First]);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Contains(world.Host.Text(reasonKey, "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(before, await world.Host.CourseFingerprintAsync(ct));
        var row = Assert.Single(await world.Host.AuditRowsAsync(ct), r => r.Action == "courses_confirmed_deleted");
        Assert.Equal(("refused", "read_only_mode"), (row.Outcome, row.RefusalCategory));
    }

    /// <summary>VR-003, SC-4, API-7: without the antiforgery token the post is <c>400</c> with the expired-page text.</summary>
    [Fact]
    public async Task WithoutTheAntiforgeryToken_ThePostIs400_AndNothingChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await StartAsync(database, Actor.Admin, ct);
        await using var _host = world.Host;
        var before = await world.Host.CourseFingerprintAsync(ct);

        var response = await PostAsync(world, ct, withToken: false, ids: [world.First]);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(world.Host.Text(SignInTestData.TextKeys.PageExpired, "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(before, await world.Host.CourseFingerprintAsync(ct));
        Assert.Empty(await world.Host.ConfirmedIdsAsync(ct));
    }

    /// <summary>
    /// VR-001, api-design §2.6: no id, or an id that is not a positive integer — <c>400</c> with the page re-rendered as it
    /// now is (the three courses still listed); nothing changes, nothing is audited.
    /// </summary>
    [Theory]
    [InlineData(new object[] { new string[0] })]
    [InlineData(new object[] { new[] { "abc" } })]
    [InlineData(new object[] { new[] { "0" } })]
    public async Task AMalformedForm_Is400_WithThePage_AndChangesNothing(string[] values)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await StartAsync(database, Actor.Admin, ct);
        await using var _host = world.Host;
        var before = await world.Host.CourseFingerprintAsync(ct);

        var response = await PostAsync(world, ct, ids: values.Cast<object>().ToArray());

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(Uk.PageTitle, response.Text, StringComparison.Ordinal);
        Assert.Equal(3, CheckboxValues(response.Body).Count);
        Assert.Equal(before, await world.Host.CourseFingerprintAsync(ct));
        Assert.Empty(await world.Host.ConfirmedIdsAsync(ct));
    }

    /// <summary>
    /// AC-017, VR-002, api-design §2.6: a course confirmed meanwhile — <c>409</c> with «Список змінився — оновіть
    /// сторінку» on the page as it now is; nothing changes and no audit row is written.
    /// </summary>
    [Fact]
    public async Task AStaleList_Is409_WithTheListChangedMessage_AndChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await StartAsync(database, Actor.Admin, ct);
        await using var _host = world.Host;
        await world.Host.MarkCourseAsync(world.Second, "not_in_classroom", D1, ct);
        var before = await world.Host.CourseFingerprintAsync(ct);

        // The Admin's own sign-in is audited before the post; the post itself must add no row.
        var auditBefore = await world.Host.AuditRowsAsync(ct);

        var response = await PostAsync(world, ct, ids: [world.First, world.Second]);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Contains(Uk.ListChanged, Section(response.Body, MessageHook), StringComparison.Ordinal);
        Assert.Equal(before, await world.Host.CourseFingerprintAsync(ct));
        Assert.Equal(auditBefore, await world.Host.AuditRowsAsync(ct));
    }

    /// <summary>TC-5, api-design §2.6: without a session the post redirects to sign-in; a restricted session to the change.</summary>
    [Theory]
    [InlineData(Actor.Anonymous, "/sign-in", "/sign-in")]
    [InlineData(Actor.DeanWithTemporaryPassword, ChangePasswordPath, ChangePasswordPath)]
    public async Task WithoutAFullSession_ThePostRedirects_AndChangesNothing(Actor actor, string tokenPage, string target)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await StartAsync(database, actor, ct);
        await using var _host = world.Host;
        var before = await world.Host.CourseFingerprintAsync(ct);

        var response = await PostAsync(world, ct, tokenPage: tokenPage, ids: [world.First]);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.StartsWith(target, response.LocationPath, StringComparison.Ordinal);
        Assert.Equal(before, await world.Host.CourseFingerprintAsync(ct));
    }
}
