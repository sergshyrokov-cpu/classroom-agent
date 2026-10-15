using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureHostExtensions;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureTestData;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-038 AC-001, AC-002, AC-003, AC-007, AC-008, AC-013, AC-014, AC-019, spec FR-001…FR-003, FR-008, FR-009, VR-001…VR-003,
/// S-09: the erasure page, the search, the confirmation, the erasure and the lifting as the Admin sees them. The markup hooks
/// are those of the test strategy §4.
/// </summary>
public sealed class ErasurePageTests(PostgreSqlFixture database)
{
    private const string NoExclusionEmail = "nobody@school-one.example.test";

    private const string ErasureLink = "href=\"/settings/erasure\"";

    public static TheoryData<Actor> AdminAndDean => new(Actor.Admin, Actor.Dean);

    /// <summary>AC-001, FR-001, FR-008: the page holds the search form and the lifting form, with no results yet.</summary>
    [Fact]
    public async Task ThePage_ShowsTheSearchAndTheLiftingForms()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.GetAsync(PagePath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(HasElement(page.Body, Markup.SearchForm));
        Assert.True(HasElement(page.Body, Markup.LiftForm));
        Assert.Contains("name=\"term\"", Inner(page.Body, Markup.SearchForm), StringComparison.Ordinal);
        Assert.Contains("name=\"email\"", Inner(page.Body, Markup.LiftForm), StringComparison.Ordinal);
        Assert.True(Html.HasInput(page.Body, Html.AntiforgeryFieldName));
        Assert.False(HasElement(page.Body, Markup.NoneFound));
        Assert.False(HasElement(page.Body, Markup.Truncated));
        Assert.Empty(ResultIds(page.Body));
    }

    /// <summary>AC-001, FR-001, FR-002: a search by name lists the person, linked to the confirmation, and keeps the term.</summary>
    [Fact]
    public async Task ASearchByName_ListsThePerson_LinkingToTheConfirmation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);

        var page = await client.PostSearchAsync("Коваль", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Null(page.Location);
        Assert.Equal([world.Target], ResultIds(page.Body));
        Assert.Contains(TargetFullName, page.Text, StringComparison.Ordinal);
        Assert.Contains(TargetEmail, page.Text, StringComparison.Ordinal);
        Assert.Contains("href=\"" + ConfirmationPath(world.Target) + "\"", page.Body, StringComparison.Ordinal);
        Assert.Contains("Коваль", Html.InputValue(page.Body, Fields.Term), StringComparison.Ordinal);
    }

    /// <summary>AC-001, FR-002: a search that matches nobody says so.</summary>
    [Fact]
    public async Task ASearchWithNoMatch_SaysNobodyFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.PostSearchAsync("Нікого Немає", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(HasElement(page.Body, Markup.NoneFound));
        Assert.Empty(ResultIds(page.Body));
    }

    /// <summary>AC-002, FR-002: more than 50 matches show 50 and ask to refine the term.</summary>
    [Fact]
    public async Task MoreThan50Matches_Show50_AndAskToRefine()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);
        for (var i = 1; i <= 51; i++)
        {
            var id = await host.InsertPersonAsync(100 + i, $"Учень{i:D3}", "Тестовий", CourseTestData.Email($"pupil{i:D3}"), ct);
            await host.AddStudentAsync(world.Physics, id, ct);
        }

        var page = await client.PostSearchAsync("учень", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(50, ResultIds(page.Body).Count);
        Assert.True(HasElement(page.Body, Markup.Truncated));
    }

    /// <summary>VR-001, FR-002: an empty, one-character or blank term is 400 with the field error and no results.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("   ")]
    public async Task AnInvalidTerm_Is400_WithTheFieldError(string term)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.PostSearchAsync(term, ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.True(HasElement(page.Body, Markup.SearchError));
        Assert.Empty(ResultIds(page.Body));
    }

    /// <summary>S-09, FR-002: a term in the query string is not a search (the term travels in the body only).</summary>
    [Fact]
    public async Task AQueryStringTerm_IsIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedErasureWorldAsync(ct);

        var page = await client.GetAsync(PagePath + "?term=" + Uri.EscapeDataString("Коваль"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.DoesNotContain("Коваль", page.Text, StringComparison.Ordinal);
        Assert.Empty(ResultIds(page.Body));
    }

    /// <summary>AC-003, FR-003: the confirmation shows what will be erased and changes nothing.</summary>
    [Fact]
    public async Task TheConfirmation_ShowsWhatWillBeErased()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);
        var before = await host.ErasureFingerprintAsync(ct);

        var page = await client.GetAsync(ConfirmationPath(world.Target), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(HasElement(page.Body, Markup.Confirmation));
        Assert.Contains(TargetFullName, page.Text, StringComparison.Ordinal);
        Assert.Contains(TargetEmail, page.Text, StringComparison.Ordinal);
        Assert.Contains(PhysicsName, page.Text, StringComparison.Ordinal);
        Assert.Contains(ChemistryName, page.Text, StringComparison.Ordinal);
        Assert.Equal(Expected.Submissions, Count(page.Body, Counts.Submissions));
        Assert.Equal(Expected.Grades, Count(page.Body, Counts.Grades));
        Assert.Equal(Expected.MeetParticipations, Count(page.Body, Counts.MeetParticipations));
        Assert.Equal(Expected.OrganizedMeetings, Count(page.Body, Counts.OrganizedMeetings));
        Assert.Equal(Expected.OwnedCourses, Count(page.Body, Counts.OwnedCourses));
        Assert.True(HasElement(page.Body, Markup.ExportWarning));
        Assert.False(HasElement(page.Body, Markup.NoEmailNotice));
        Assert.True(HasElement(page.Body, Markup.EraseForm));
        Assert.True(Html.HasInput(page.Body, Fields.Mark));
        Assert.Contains("type=\"checkbox\"", Inner(page.Body, Markup.EraseForm), StringComparison.Ordinal);
        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
    }

    /// <summary>AC-013, OD-001: the confirmation of a participant with no email carries the notice.</summary>
    [Fact]
    public async Task TheConfirmationOfAParticipantWithNoEmail_ShowsTheNotice()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);
        var nameless = await host.InsertPersonAsync(9, null, null, null, ct);
        await host.AddStudentAsync(world.Physics, nameless, ct);

        var page = await client.GetAsync(ConfirmationPath(nameless), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(HasElement(page.Body, Markup.NoEmailNotice));
    }

    /// <summary>AC-001, FR-009: a confirmed erasure redirects to the page with a message; the person is gone and not named.</summary>
    [Fact]
    public async Task ConfirmedErasure_RedirectsToThePage_WithTheMessage_AndThePersonIsGone()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);

        var erasure = await client.PostErasureAsync(world.Target, "true", ct);

        Assert.Equal(HttpStatusCode.Redirect, erasure.Status);
        Assert.Equal(PagePath, erasure.LocationPath);
        Assert.DoesNotContain("koval", erasure.LocationPath, StringComparison.OrdinalIgnoreCase);
        var page = await client.GetAsync(PagePath, ct);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(HasElement(page.Body, Markup.Message));
        Assert.DoesNotContain(TargetEmail, page.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            0L,
            await host.ScalarAsync<long>(
                "SELECT count(*) FROM classroom_participant WHERE id = @id", ct, ("id", world.Target)));
    }

    /// <summary>FR-009: the one-time message is shown once.</summary>
    [Fact]
    public async Task TheMessage_IsShownOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);
        var erasure = await client.PostErasureAsync(world.Target, "true", ct);
        Assert.Equal(HttpStatusCode.Redirect, erasure.Status);

        var first = await client.GetAsync(PagePath, ct);
        var second = await client.GetAsync(PagePath, ct);

        Assert.True(HasElement(first.Body, Markup.Message));
        Assert.False(HasElement(second.Body, Markup.Message));
    }

    /// <summary>AC-003, VR-002: without the mark the erasure is 400, the confirmation is shown again and nothing changes.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("on")]
    [InlineData("false")]
    public async Task WithoutTheMark_TheErasureIs400_AndNothingChanges(string? mark)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var world = await host.SeedErasureWorldAsync(ct);
        var before = await host.ErasureFingerprintAsync(ct);

        var page = await client.PostErasureAsync(world.Target, mark, ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.True(HasElement(page.Body, Markup.MarkError));
        Assert.True(HasElement(page.Body, Markup.Confirmation));
        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
        Assert.Empty(await host.ErasureAuditRowsAsync(ct));
    }

    /// <summary>AC-014, FR-008, VR-003: lifting by email in any letter case redirects with the message and removes the exclusion.</summary>
    [Fact]
    public async Task LiftingByEmailInAnyCase_RedirectsWithTheMessage_AndRemovesTheExclusion()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedExclusionAsync(TargetGoogleUserId, TargetEmail, ct);

        var lifting = await client.PostLiftingAsync(TargetEmail.ToUpperInvariant(), ct);

        Assert.Equal(HttpStatusCode.Redirect, lifting.Status);
        Assert.Equal(PagePath, lifting.LocationPath);
        var page = await client.GetAsync(PagePath, ct);
        Assert.True(HasElement(page.Body, Markup.Message));
        Assert.DoesNotContain(TargetEmail, page.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await host.ExclusionRowsAsync(ct));
    }

    /// <summary>AC-014, FR-008: an email with no exclusion says so, keeps the typed email and changes nothing.</summary>
    [Fact]
    public async Task LiftingAnEmailWithNoExclusion_SaysSo_KeepsTheEmail_AndChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedErasureWorldAsync(ct);
        var before = await host.ErasureFingerprintAsync(ct);

        var page = await client.PostLiftingAsync(NoExclusionEmail, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.True(HasElement(page.Body, Markup.NoExclusion));
        Assert.Equal(NoExclusionEmail, Html.InputValue(page.Body, Fields.Email));
        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
        Assert.Empty(await host.ErasureAuditRowsAsync(ct));
    }

    /// <summary>VR-003: an empty or malformed email is 400 with the field error.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("a@b@c.test")]
    public async Task AnInvalidEmail_Is400_WithTheFieldError(string email)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.PostLiftingAsync(email, ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.True(HasElement(page.Body, Markup.LiftError));
    }

    /// <summary>AC-019, FR-001: the home page links to the erasure page for the Admin only.</summary>
    [Theory]
    [MemberData(nameof(AdminAndDean))]
    public async Task TheHomePage_LinksToTheErasurePage_ForTheAdminOnly(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, actor, ct);
        await using var _host = host;

        var page = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        if (actor == Actor.Admin)
        {
            var block = Inner(page.Body, Markup.AdminSettings);
            Assert.NotNull(block);
            Assert.Contains(ErasureLink, block, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain(ErasureLink, page.Body, StringComparison.Ordinal);
        }
    }
}
