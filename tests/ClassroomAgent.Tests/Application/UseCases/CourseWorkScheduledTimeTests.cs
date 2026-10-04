using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-027 spec FR-012, AC-012, db-design §3: <see cref="CourseWork.ScheduledTime"/> is stored on import and replaced on
/// update — also with null, when Google no longer sends it — for work and for materials alike.
/// </summary>
public sealed class CourseWorkScheduledTimeTests
{
    private static readonly DateTimeOffset ItemDate = InstallationTestHost.DefaultStart;

    private static readonly DateTimeOffset First = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Second = new(2026, 4, 2, 9, 30, 0, TimeSpan.Zero);

    private static CourseWorkDetails Details(DateTimeOffset? scheduled) =>
        new(CourseWorkTestData.Title(1), ItemDate, null, null, null, null, ScheduledTime: scheduled);

    /// <summary>AC-012: stored on import, replaced on update, cleared by a null.</summary>
    [Theory]
    [InlineData(CourseWorkResource.CourseWork)]
    [InlineData(CourseWorkResource.CourseWorkMaterial)]
    public void TheScheduledTime_IsStoredOnImportAndUpdate(CourseWorkResource resource)
    {
        var item = CourseWork.Import(1, CourseWorkTestData.ItemId(1), resource, Details(First));
        Assert.Equal(First, item.ScheduledTime);

        item.UpdateFrom(Details(Second));
        Assert.Equal(Second, item.ScheduledTime);

        item.UpdateFrom(Details(null));
        Assert.Null(item.ScheduledTime);
    }

    /// <summary>AC-012: an item imported without a scheduled time has none.</summary>
    [Fact]
    public void AnItemWithoutAScheduledTime_HasNone()
    {
        var item = CourseWork.Import(1, CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, Details(null));

        Assert.Null(item.ScheduledTime);
    }
}
