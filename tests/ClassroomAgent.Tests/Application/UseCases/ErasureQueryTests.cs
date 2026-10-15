using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-038 spec FR-002, FR-003, VR-001, I-4, I-8, OD-001 a; AC-001, AC-013, AC-014, AC-017, AC-019: the search validates
/// its term and bounds its results, and the confirmation maps what the source read — both with the read source
/// substituted (TC-1). Neither writes anything.
/// </summary>
public sealed class ErasureQueryTests
{
    public static TheoryData<string?> RequiredTerms => new(null, string.Empty, "    ");

    private static ParticipantSearchRecord Row(int i) =>
        new(i, $"Person {i:D3}", $"person{i:D3}@school-one.example.test", i % 2, 1);

    /// <summary>VR-001: a missing or blank term is required; no search runs.</summary>
    [Theory]
    [MemberData(nameof(RequiredTerms))]
    public async Task AMissingTerm_IsRequired_AndNoSearchRuns(string? term)
    {
        var world = new ErasureUnitWorld();

        var block = await world.Search.ExecuteAsync(term, TestContext.Current.CancellationToken);

        Assert.Equal(SearchFieldError.TermRequired, block.FieldError);
        Assert.False(block.Performed);
        Assert.Empty(block.Results);
        Assert.Empty(world.Reads.Searches);
    }

    /// <summary>VR-001: one character after trimming is too short.</summary>
    [Theory]
    [InlineData("a")]
    [InlineData("  Ж  ")]
    public async Task AOneCharacterTerm_IsTooShort(string term)
    {
        var world = new ErasureUnitWorld();

        var block = await world.Search.ExecuteAsync(term, TestContext.Current.CancellationToken);

        Assert.Equal(SearchFieldError.TermTooShort, block.FieldError);
        Assert.Empty(world.Reads.Searches);
    }

    /// <summary>VR-001: 255 characters after trimming is too long; 254 is accepted.</summary>
    [Fact]
    public async Task TheTermLengthBound_Is254()
    {
        var world = new ErasureUnitWorld();
        var ct = TestContext.Current.CancellationToken;

        var tooLong = await world.Search.ExecuteAsync(new string('x', 255), ct);
        var longest = await world.Search.ExecuteAsync(" " + new string('x', 254) + " ", ct);

        Assert.Equal(SearchFieldError.TermTooLong, tooLong.FieldError);
        Assert.Null(longest.FieldError);
        Assert.True(longest.Performed);
        Assert.Equal(new string('x', 254), Assert.Single(world.Reads.Searches).Term);
    }

    /// <summary>FR-002, I-4: the source is asked for the trimmed term and 51 rows, the 51st telling that more matched.</summary>
    [Fact]
    public async Task TheSource_IsAskedForTheTrimmedTerm_AndOneRowMoreThanShown()
    {
        var world = new ErasureUnitWorld();

        await world.Search.ExecuteAsync("  Коваль ", TestContext.Current.CancellationToken);

        Assert.Equal(("Коваль", SearchParticipantsForErasureQuery.MaxResults + 1), Assert.Single(world.Reads.Searches));
        Assert.Equal(50, SearchParticipantsForErasureQuery.MaxResults);
    }

    /// <summary>AC-014: 51 matches — the first 50 shown and the "refine" flag set.</summary>
    [Fact]
    public async Task MoreThan50Matches_Show50_AndAreTruncated()
    {
        var world = new ErasureUnitWorld();
        world.Reads.SearchRows.AddRange(Enumerable.Range(1, 51).Select(Row));

        var block = await world.Search.ExecuteAsync("Person", TestContext.Current.CancellationToken);

        Assert.True(block.Truncated);
        Assert.Equal(50, block.Results.Count);
        Assert.Equal(Enumerable.Range(1, 50).Select(i => (long)i), block.Results.Select(r => r.ParticipantId));
    }

    /// <summary>AC-014: exactly 50 matches are all shown and not truncated.</summary>
    [Fact]
    public async Task Exactly50Matches_AreNotTruncated()
    {
        var world = new ErasureUnitWorld();
        world.Reads.SearchRows.AddRange(Enumerable.Range(1, 50).Select(Row));

        var block = await world.Search.ExecuteAsync("Person", TestContext.Current.CancellationToken);

        Assert.False(block.Truncated);
        Assert.Equal(50, block.Results.Count);
    }

    /// <summary>FR-002: no match — performed, empty ("nobody found").</summary>
    [Fact]
    public async Task NoMatch_IsPerformed_AndEmpty()
    {
        var world = new ErasureUnitWorld();

        var block = await world.Search.ExecuteAsync("Нікого", TestContext.Current.CancellationToken);

        Assert.True(block.Performed);
        Assert.Null(block.FieldError);
        Assert.Empty(block.Results);
        Assert.False(block.Truncated);
        Assert.NotNull(block.Term);
    }

    /// <summary>AC-013, FR-002: each result carries the id, the name and email as stored, and the course count per role.</summary>
    [Fact]
    public async Task AResult_CarriesTheIdNameEmailAndCoursesPerRole()
    {
        var world = new ErasureUnitWorld();
        world.Reads.SearchRows.Add(new ParticipantSearchRecord(9, null, null, 2, 0));

        var block = await world.Search.ExecuteAsync("10000", TestContext.Current.CancellationToken);

        Assert.Equal(new ParticipantSearchResult(9, null, null, 2, 0), Assert.Single(block.Results));
    }

    /// <summary>AC-015, FR-016: an unknown id has no confirmation.</summary>
    [Fact]
    public async Task TheConfirmationOfAnUnknownId_IsNull()
    {
        var world = new ErasureUnitWorld();

        Assert.Null(await world.Confirmation().ExecuteAsync(5, TestContext.Current.CancellationToken));
    }

    /// <summary>AC-001, FR-003: the courses with role, the five counts, the email and name as stored; no mark error.</summary>
    [Fact]
    public async Task TheConfirmation_MapsTheCoursesAndCounts()
    {
        var world = new ErasureUnitWorld();
        world.Reads.Confirmations[5] = new ErasureConfirmationData(
            5,
            "Олена Коваль",
            "o.koval@school-one.example.test",
            [new ErasureCourseRecord("Фізика 9-А", ClassroomRole.Student), new ErasureCourseRecord("Хімія 9-А", ClassroomRole.Teacher)],
            3,
            2,
            4,
            1,
            1);

        var page = await world.Confirmation().ExecuteAsync(5, TestContext.Current.CancellationToken);

        Assert.NotNull(page);
        Assert.Equal(5, page.ParticipantId);
        Assert.Equal("Олена Коваль", page.FullName);
        Assert.Equal("o.koval@school-one.example.test", page.Email);
        Assert.Equal(
            [new ErasureCourseEntry("Фізика 9-А", ErasureCourseRole.Student), new ErasureCourseEntry("Хімія 9-А", ErasureCourseRole.Teacher)],
            page.Courses);
        Assert.Equal((3, 2, 4, 1, 1), (page.Submissions, page.Grades, page.MeetParticipations, page.OrganizedMeetings, page.OwnedCourses));
        Assert.False(page.NoEmailNotice);
        Assert.False(page.ReadOnlyNotice);
        Assert.Null(page.MarkError);
    }

    /// <summary>AC-019, OD-001 a: no email — the notice is set.</summary>
    [Fact]
    public async Task TheConfirmationOfAParticipantWithNoEmail_SetsTheNotice()
    {
        var world = new ErasureUnitWorld();
        world.Reads.Confirmations[6] = new ErasureConfirmationData(6, "Без Адреси", null, [], 0, 0, 0, 0, 0);

        var page = await world.Confirmation().ExecuteAsync(6, TestContext.Current.CancellationToken);

        Assert.True(page!.NoEmailNotice);
    }

    /// <summary>AC-017, I-8: in read-only mode the confirmation still opens, with the notice (display only).</summary>
    [Fact]
    public async Task InReadOnlyMode_TheConfirmationOpens_WithTheNotice()
    {
        var world = new ErasureUnitWorld();
        world.Reads.Confirmations[5] = new ErasureConfirmationData(5, "Олена Коваль", "o.koval@school-one.example.test", [], 0, 0, 0, 0, 0);

        var page = await world.Confirmation(readOnlyState: true).ExecuteAsync(5, TestContext.Current.CancellationToken);

        Assert.True(page!.ReadOnlyNotice);
    }

    /// <summary>FR-002, FR-003: the queries write nothing.</summary>
    [Fact]
    public async Task TheQueries_TouchNoWritingPort()
    {
        var world = new ErasureUnitWorld();
        world.Reads.SearchRows.Add(Row(1));
        world.Reads.Confirmations[1] = new ErasureConfirmationData(1, "P", "p@school-one.example.test", [], 0, 0, 0, 0, 0);
        var ct = TestContext.Current.CancellationToken;

        await world.Search.ExecuteAsync("Person", ct);
        await world.Confirmation().ExecuteAsync(1, ct);

        Assert.Equal(["search", "confirmation"], world.Calls);
    }
}
