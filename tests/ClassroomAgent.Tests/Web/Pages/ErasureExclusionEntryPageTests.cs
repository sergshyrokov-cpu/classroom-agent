using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureHostExtensions;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureTestData;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-065 AC-001, AC-002, AC-006, AC-007, AC-011, AC-014, AC-019, spec FR-001, FR-003, FR-004, FR-005, VR-001, S-08: the
/// "Exclude by email" block of the erasure page as the Admin sees it.
/// </summary>
public sealed class ErasureExclusionEntryPageTests(PostgreSqlFixture database)
{
    public static TheoryData<string> InvalidEmails =>
        new("", "not-an-email", "a@b@c.test", new string('a', 255 - "@school.test".Length) + "@school.test");

    /// <summary>AC-002, FR-001: the page holds the entry form with no Google connection saved.</summary>
    [Fact]
    public async Task ThePage_ShowsTheEntryForm_WithNoGoogleConnectionSaved()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.GetAsync(PagePath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(0L, await host.CountAsync("workspace_connection", ct));
        Assert.True(HasElement(page.Body, Markup.EnterForm));
        Assert.Contains("name=\"email\"", Inner(page.Body, Markup.EnterForm), StringComparison.Ordinal);
        Assert.Contains("action=\"/settings/erasure/exclusions\"", page.Body, StringComparison.Ordinal);
        Assert.True(Html.HasInput(page.Body, Html.AntiforgeryFieldName));
    }

    /// <summary>AC-001, FR-003, S-08: an entry redirects to the page with a one-time message and stores the exclusion.</summary>
    [Fact]
    public async Task AnEntry_RedirectsToThePage_WithTheMessageOnce_AndStoresTheExclusion()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var entry = await client.PostEntryAsync(TargetEmail, ct);

        Assert.Equal(HttpStatusCode.Redirect, entry.Status);
        Assert.Equal(PagePath, entry.LocationPath);
        Assert.DoesNotContain("koval", entry.Location ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        var first = await client.GetAsync(PagePath, ct);
        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.True(HasElement(first.Body, Markup.Message));
        Assert.Contains(host.Text(Keys.ExclusionEntered, "uk"), first.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(TargetEmail, first.Text, StringComparison.OrdinalIgnoreCase);
        var second = await client.GetAsync(PagePath, ct);
        Assert.False(HasElement(second.Body, Markup.Message));
        var row = Assert.Single(await host.ExclusionRowsAsync(ct));
        Assert.Null(row.GoogleUserId);
    }

    /// <summary>AC-006, AC-014, FR-004: an email already loaded is 200 "already loaded", listing the people with links, and changes nothing.</summary>
    [Fact]
    public async Task ALoadedEmail_Is200_AlreadyLoaded_ListingThePeople_WithLinks()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);
        var before = await host.ErasureFingerprintAsync(ct);

        var page = await client.PostEntryAsync(TargetEmail.ToUpperInvariant(), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(HasElement(page.Body, Markup.AlreadyLoaded));
        Assert.Equal([world.Target], ResultIds(Inner(page.Body, Markup.LoadedResults)!));
        Assert.Contains("href=\"" + ConfirmationPath(world.Target) + "\"", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(TargetEmail, Inner(page.Body, Markup.EnterForm)!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
        Assert.Empty(await host.ErasureAuditRowsAsync(ct));
    }

    /// <summary>AC-007, FR-005: an email already excluded is 200 "already excluded" and nothing changes.</summary>
    [Fact]
    public async Task AnExcludedEmail_Is200_AlreadyExcluded_AndNothingChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedExclusionInAdvanceAsync(TargetEmail, ct);
        var before = await host.ErasureFingerprintAsync(ct);

        var page = await client.PostEntryAsync(TargetEmailOtherCase, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(HasElement(page.Body, Markup.AlreadyExcluded));
        Assert.False(HasElement(page.Body, Markup.AlreadyLoaded));
        Assert.DoesNotContain("koval", Inner(page.Body, Markup.EnterForm)!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
    }

    /// <summary>AC-011, VR-001: an empty, malformed or too long email is 400 with the field error and stores nothing.</summary>
    [Theory]
    [MemberData(nameof(InvalidEmails))]
    public async Task AnInvalidEmail_Is400_WithTheFieldError_AndKeepsTheValue(string email)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.PostEntryAsync(email, ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.True(HasElement(page.Body, Markup.EnterError));
        Assert.False(HasElement(page.Body, Markup.LiftError));
        if (email.Length > 0)
        {
            Assert.Contains(email, Inner(page.Body, Markup.EnterForm)!, StringComparison.Ordinal);
        }

        Assert.Empty(await host.ExclusionRowsAsync(ct));
        Assert.Empty(await host.ErasureAuditRowsAsync(ct));
    }

    /// <summary>AC-019: in read-only mode the entry block shows the notice and a disabled button.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_TheEntryBlock_ShowsTheNotice_AndADisabledButton(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct, cause);
        await using var _host = host;

        var page = await client.GetAsync(PagePath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(HasElement(page.Body, Markup.ReadOnlyNotice));
        Assert.Contains("disabled", Inner(page.Body, Markup.EnterForm), StringComparison.Ordinal);
    }

    /// <summary>AC-019: outside read-only mode the entry button is enabled (so the check above is not vacuous).</summary>
    [Fact]
    public async Task OutsideReadOnlyMode_TheEntryButtonIsEnabled()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.GetAsync(PagePath, ct);

        var form = Inner(page.Body, Markup.EnterForm);
        Assert.NotNull(form);
        Assert.DoesNotContain("disabled", form, StringComparison.Ordinal);
    }

    /// <summary>S-08: an email in the query string is not an entry.</summary>
    [Fact]
    public async Task AQueryStringEmail_IsNotAnEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.GetAsync(PagePath + "?email=" + Uri.EscapeDataString(TargetEmail), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Empty(await host.ExclusionRowsAsync(ct));
        Assert.DoesNotContain(TargetEmail, page.Text, StringComparison.Ordinal);
    }
}
