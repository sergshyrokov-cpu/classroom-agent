using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-022 api-design §2.7, spec FR-009, S-05, S-06: the one rule for teacher-list URLs — <c>status</c> then <c>page</c>,
/// each omitted at its default, lower-case tokens, nothing else.
/// </summary>
public sealed class TeacherListUrlTests
{
    public static TheoryData<CourseStatusFilter, int, string> Cases => new()
    {
        { CourseStatusFilter.Active, 1, "/workspace/teachers" },
        { CourseStatusFilter.Active, 3, "/workspace/teachers?page=3" },
        { CourseStatusFilter.Archived, 1, "/workspace/teachers?status=archived" },
        { CourseStatusFilter.Provisioned, 1, "/workspace/teachers?status=provisioned" },
        { CourseStatusFilter.Declined, 1, "/workspace/teachers?status=declined" },
        { CourseStatusFilter.Suspended, 2, "/workspace/teachers?status=suspended&page=2" },
        { CourseStatusFilter.All, 100000, "/workspace/teachers?status=all&page=100000" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Build_WritesStatusThenPage_OmittingDefaults(CourseStatusFilter status, int page, string expected) =>
        Assert.Equal(expected, TeacherListUrl.Build(status, page));

    [Fact]
    public void ThePath_IsTheTeacherListRoute() =>
        Assert.Equal(TeacherListTestData.Path, TeacherListUrl.Path);
}
