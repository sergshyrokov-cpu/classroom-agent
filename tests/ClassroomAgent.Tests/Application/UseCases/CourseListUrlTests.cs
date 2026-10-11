using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-020 AC-008, AC-010 (spec FR-009, S-05; openapi <c>CourseListUrl</c>): the one rule behind every URL the page
/// produces — the path, the parameter order, defaults omitted, the lower-case status token, the decimal teacher id, the
/// search text percent-encoded as UTF-8, and nothing else.
/// </summary>
public sealed class CourseListUrlTests
{
    public static TheoryData<CourseStatusFilter, string> Tokens => new()
    {
        { CourseStatusFilter.Archived, "archived" },
        { CourseStatusFilter.Provisioned, "provisioned" },
        { CourseStatusFilter.Declined, "declined" },
        { CourseStatusFilter.Suspended, "suspended" },
        { CourseStatusFilter.All, "all" },
    };

    [Fact]
    public void ThePath_IsTheRoute()
    {
        Assert.Equal(CourseListTestData.Path, CourseListUrl.Path);
    }

    /// <summary>Every default omitted: ACTIVE, no teacher, no search, page 1.</summary>
    [Fact]
    public void TheDefaults_GiveThePathAlone()
    {
        Assert.Equal(CourseListTestData.Path, CourseListUrl.Build(CourseStatusFilter.Active, null, null, 1));
    }

    [Theory]
    [MemberData(nameof(Tokens))]
    public void AStatus_IsItsLowerCaseToken(CourseStatusFilter status, string token)
    {
        Assert.Equal(CourseListTestData.Path + "?status=" + token, CourseListUrl.Build(status, null, null, 1));
    }

    /// <summary>All four, in the order status, teacherId, q, page.</summary>
    [Fact]
    public void AllFour_AreInTheCanonicalOrder()
    {
        Assert.Equal(
            "/workspace/courses?status=archived&teacherId=42&q=%D1%84%D1%96%D0%B7%D0%B8%D0%BA%D0%B0&page=3",
            CourseListUrl.Build(CourseStatusFilter.Archived, 42, "фізика", 3));
    }

    [Fact]
    public void OnlyTheTeacher_GivesOnlyTheTeacher()
    {
        Assert.Equal("/workspace/courses?teacherId=9223372036854775807", CourseListUrl.Build(CourseStatusFilter.Active, long.MaxValue, null, 1));
    }

    [Fact]
    public void OnlyThePage_GivesOnlyThePage()
    {
        Assert.Equal("/workspace/courses?page=2", CourseListUrl.Build(CourseStatusFilter.Active, null, null, 2));
    }

    /// <summary>S-09, VR-005: characters with a meaning in a URL are encoded, so the search cannot add a parameter.</summary>
    [Theory]
    [InlineData("a&b=c", "a%26b%3Dc")]
    [InlineData("10 %", "10%20%25")]
    [InlineData("C++ #1", "C%2B%2B%20%231")]
    [InlineData("’?/", "%E2%80%99%3F%2F")]
    public void TheSearchText_IsPercentEncoded(string text, string encoded)
    {
        Assert.Equal("/workspace/courses?q=" + encoded, CourseListUrl.Build(CourseStatusFilter.Active, null, text, 1));
    }

    /// <summary>An absent or empty search text is omitted.</summary>
    [Fact]
    public void AnEmptySearchText_IsOmitted()
    {
        Assert.Equal(CourseListTestData.Path, CourseListUrl.Build(CourseStatusFilter.Active, null, string.Empty, 1));
    }
}
