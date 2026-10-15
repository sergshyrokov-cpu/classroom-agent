using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureHostExtensions;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-038 AC-001, AC-013, AC-014, AC-019 and FR-002, FR-003, VR-001: the search and the confirmation over the real read
/// source and PostgreSQL (TC-2), resolved from the host's container. Neither writes anything.
/// </summary>
public sealed class ErasureSearchTests(PostgreSqlFixture database)
{
    private async Task<InstallationTestHost> StartAsync(CancellationToken ct)
    {
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        client.Dispose();
        return host;
    }

    /// <summary>AC-013: part of the surname, in another letter case, finds the person.</summary>
    [Theory]
    [InlineData("коВАЛ")]
    [InlineData("Олена")]
    [InlineData("олена ков")]
    [InlineData("O.KOV")]
    [InlineData("koval@school-one")]
    public async Task APartOfANameOrEmail_InAnyLetterCase_FindsThePerson(string term)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);

        var block = await host.SearchAsync(term, ct);

        Assert.True(block.Performed);
        var found = Assert.Single(block.Results);
        Assert.Equal(new ParticipantSearchResult(world.Target, TargetFullName, TargetEmail, 0, 2), found);
    }

    /// <summary>FR-002: a person on no roster any more still holds data and is found.</summary>
    [Fact]
    public async Task APersonOffEveryRoster_IsFound()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);
        var leaver = await host.InsertPersonAsync(10, "Франко", "Іван", CourseTestData.Email("i.franko"), ct);
        await host.AddStudentAsync(world.Physics, leaver, ct, onRoster: false);
        await host.AddTeacherAsync(world.History, leaver, ct, onRoster: false);

        var block = await host.SearchAsync("франко", ct);

        Assert.Equal(new ParticipantSearchResult(leaver, "Іван Франко", CourseTestData.Email("i.franko"), 1, 1), Assert.Single(block.Results));
    }

    /// <summary>VR-001: the term is literal — "%", "_" and "\" match only themselves.</summary>
    [Theory]
    [InlineData("о%")]
    [InlineData("к_валь")]
    [InlineData("%%")]
    [InlineData("\\%")]
    public async Task WildcardCharacters_AreTakenLiterally(string term)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        await host.SeedErasureWorldAsync(ct);

        var block = await host.SearchAsync(term, ct);

        Assert.True(block.Performed);
        Assert.Empty(block.Results);
    }

    /// <summary>VR-001: a name that really contains "%" is found by it.</summary>
    [Fact]
    public async Task ALiteralPercent_FindsANameContainingIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);
        var odd = await host.InsertPersonAsync(11, "Сто%", "Відсоток", null, ct);
        await host.AddStudentAsync(world.Physics, odd, ct);

        var block = await host.SearchAsync("о%", ct);

        Assert.Equal(odd, Assert.Single(block.Results).ParticipantId);
    }

    /// <summary>FR-002: results are ordered by full name, then email, then internal id.</summary>
    [Fact]
    public async Task Results_AreOrderedByFullNameThenEmailThenId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);
        var b2 = await host.InsertPersonAsync(21, "Тест", "Борис", CourseTestData.Email("test.b2"), ct);
        var a = await host.InsertPersonAsync(22, "Тест", "Антон", CourseTestData.Email("test.z"), ct);
        var b1 = await host.InsertPersonAsync(23, "Тест", "Борис", CourseTestData.Email("test.b1"), ct);
        var b1Twin = await host.InsertPersonAsync(24, "Тест", "Борис", CourseTestData.Email("test.b1"), ct);
        foreach (var id in new[] { b2, a, b1, b1Twin })
        {
            await host.AddStudentAsync(world.Physics, id, ct);
        }

        var block = await host.SearchAsync("тест", ct);

        Assert.Equal([a, b1, b1Twin, b2], block.Results.Select(r => r.ParticipantId));
    }

    /// <summary>AC-014: 51 matches — 50 shown, in order, and the "refine" flag.</summary>
    [Fact]
    public async Task MoreThan50Matches_Show50_AndAreTruncated()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);
        var ids = new List<long>();
        for (var i = 1; i <= 51; i++)
        {
            var id = await host.InsertPersonAsync(100 + i, $"Учень{i:D3}", "Тестовий", CourseTestData.Email($"pupil{i:D3}"), ct);
            await host.AddStudentAsync(world.Physics, id, ct);
            ids.Add(id);
        }

        var block = await host.SearchAsync("учень", ct);

        Assert.True(block.Truncated);
        Assert.Equal(ids.Take(50), block.Results.Select(r => r.ParticipantId));
    }

    /// <summary>FR-002: no match — performed, empty.</summary>
    [Fact]
    public async Task NoMatch_IsPerformedAndEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        await host.SeedErasureWorldAsync(ct);

        var block = await host.SearchAsync("Нікого Немає", ct);

        Assert.True(block.Performed);
        Assert.Empty(block.Results);
        Assert.False(block.Truncated);
    }

    /// <summary>AC-001, FR-003: the confirmation counts what the erasure will touch, Meet rows in any letter case.</summary>
    [Fact]
    public async Task TheConfirmation_ShowsTheCoursesAndTheFiveCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);

        var page = await host.ConfirmationAsync(world.Target, ct);

        Assert.NotNull(page);
        Assert.Equal((TargetFullName, TargetEmail), (page.FullName, page.Email));
        Assert.Equal(
            [new ErasureCourseEntry(PhysicsName, ErasureCourseRole.Student), new ErasureCourseEntry(ChemistryName, ErasureCourseRole.Student)],
            page.Courses);
        Assert.Equal(
            (Expected.Submissions, Expected.Grades, Expected.MeetParticipations, Expected.OrganizedMeetings, Expected.OwnedCourses),
            (page.Submissions, page.Grades, page.MeetParticipations, page.OrganizedMeetings, page.OwnedCourses));
        Assert.False(page.NoEmailNotice);
        Assert.False(page.ReadOnlyNotice);
    }

    /// <summary>AC-019: no email — no Meet row counted, and the notice set.</summary>
    [Fact]
    public async Task TheConfirmationOfAParticipantWithNoEmail_CountsNoMeetRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);
        var nameless = await host.InsertPersonAsync(9, null, null, null, ct);
        await host.AddStudentAsync(world.Physics, nameless, ct);

        var page = await host.ConfirmationAsync(nameless, ct);

        Assert.NotNull(page);
        Assert.True(page.NoEmailNotice);
        Assert.Equal((0, 0), (page.MeetParticipations, page.OrganizedMeetings));
    }

    /// <summary>AC-015: an unknown id has no confirmation.</summary>
    [Fact]
    public async Task TheConfirmationOfAnUnknownId_IsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        await host.SeedErasureWorldAsync(ct);

        Assert.Null(await host.ConfirmationAsync(987_654, ct));
    }

    /// <summary>FR-002, FR-003: searching and opening a confirmation write nothing.</summary>
    [Fact]
    public async Task SearchAndConfirmation_WriteNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        var world = await host.SeedErasureWorldAsync(ct);
        var before = await host.ErasureFingerprintAsync(ct);

        await host.SearchAsync("Коваль", ct);
        await host.ConfirmationAsync(world.Target, ct);

        Assert.Equal(before, await host.ErasureFingerprintAsync(ct));
    }
}
