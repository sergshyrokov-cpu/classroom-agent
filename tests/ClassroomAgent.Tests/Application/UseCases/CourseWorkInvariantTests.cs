using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-015 VR-002, FR-005, FR-008: what <see cref="CourseWork"/> refuses, what it cuts, and how it derives
/// <see cref="CourseWork.Kind"/>. A title longer than its bound is <b>cut, not refused</b> — the US-014
/// <c>Course</c> precedent — while the Google id is required, because it is the upsert key (PC-3), and an item
/// with no date at all from the FR-008 cascade is refused outright (db-design §3.3).
/// </summary>
public sealed class CourseWorkInvariantTests
{
    private static readonly DateTimeOffset SomeInstant = InstallationTestHost.DefaultStart;

    private static CourseWorkDetails Details(
        string? title = null,
        DateTimeOffset? itemDate = null,
        DateTimeOffset? dueAt = null,
        decimal? maxPoints = null,
        DateTimeOffset? creationTime = null,
        DateTimeOffset? updateTime = null) =>
        new(
            title ?? CourseWorkTestData.Title(1),
            itemDate ?? SomeInstant,
            dueAt,
            maxPoints,
            creationTime,
            updateTime);

    /// <summary>VR-002: a title longer than <see cref="CourseWork.MaxTitleLength"/> is cut, and the item is still imported.</summary>
    [Fact]
    public void OverlongTitle_IsCutNotRefused()
    {
        var verbose = new string('t', CourseWork.MaxTitleLength + 500);

        var item = CourseWork.Import(1, CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, Details(title: verbose));

        Assert.Equal(CourseWork.MaxTitleLength, item.Title.Length);
        Assert.Equal(CourseWorkTestData.ItemId(1), item.GoogleId);
    }

    /// <summary>BR-052, PC-3: the material resource always derives <see cref="CourseWorkKind.Material"/>, regardless of <c>MaxPoints</c>.</summary>
    [Fact]
    public void Kind_IsMaterialForTheMaterialResource()
    {
        var item = CourseWork.Import(1, CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWorkMaterial, Details());

        Assert.Equal(CourseWorkKind.Material, item.Kind);
    }

    /// <summary>BR-052, PC-3: a <c>courseWork</c> item with maximum points set derives <see cref="CourseWorkKind.GradedWork"/>.</summary>
    [Fact]
    public void Kind_IsGradedWorkWhenMaximumPointsAreSet()
    {
        var item = CourseWork.Import(1, CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, Details(maxPoints: 100m));

        Assert.Equal(CourseWorkKind.GradedWork, item.Kind);
    }

    /// <summary>BR-052, PC-3: a <c>courseWork</c> item with no maximum points derives <see cref="CourseWorkKind.UngradedWork"/>.</summary>
    [Fact]
    public void Kind_IsUngradedWorkWhenMaximumPointsAreAbsent()
    {
        var item = CourseWork.Import(1, CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, Details(maxPoints: null));

        Assert.Equal(CourseWorkKind.UngradedWork, item.Kind);
    }

    /// <summary>VR-002, PC-3: the Google id is required — it is part of the upsert key, so an item without one is not importable.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingGoogleId_IsRefused(string googleId) =>
        Assert.Throws<ArgumentException>(() => CourseWork.Import(1, googleId, CourseWorkResource.CourseWork, Details()));

    /// <summary>
    /// db-design §3.3: an item with no date at all from the FR-008 cascade is unimportable. Because
    /// <see cref="CourseWorkDetails.ItemDate"/> is a non-nullable <see cref="DateTimeOffset"/>, the absent case is
    /// the type's own default instant — the only value that can represent "no cascade source produced anything".
    /// </summary>
    /// <remarks>
    /// The details record is built directly rather than through <see cref="Details"/>: that helper's parameter is
    /// a <c>DateTimeOffset?</c>, so passing <c>default</c> to it means <see langword="null"/> and the helper
    /// substitutes a real instant — the test would then assert nothing (IMPLEMENTATION deviation D-1).
    /// </remarks>
    [Fact]
    public void ItemWithNoCascadeDate_IsRefused() =>
        Assert.ThrowsAny<ArgumentException>(() => CourseWork.Import(
            1,
            CourseWorkTestData.ItemId(1),
            CourseWorkResource.CourseWork,
            new CourseWorkDetails(CourseWorkTestData.Title(1), default, null, null, null, null)));

    /// <summary>
    /// db-design §3.4: when Google does not carry both a due date and a due time, the adapter has already reduced
    /// <see cref="CourseWorkDetails.DueAt"/> to absent — the entity stores that as given, never guessing a
    /// midnight or end-of-day fallback.
    /// </summary>
    [Fact]
    public void DueDateWithoutATime_IsStoredAsNoDueDate()
    {
        var item = CourseWork.Import(1, CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, Details(dueAt: null));

        Assert.Null(item.DueAt);
    }
}
