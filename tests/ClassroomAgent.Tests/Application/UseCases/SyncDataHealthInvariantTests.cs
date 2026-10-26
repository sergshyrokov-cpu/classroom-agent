using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-058 entity model §1 … §3: the invariants the entities hold themselves — the "not in Classroom" mark of a
/// <see cref="CourseWork"/> (FR-001 … FR-003, VR-005), the last-read instant of a <see cref="Course"/> (FR-008) and
/// the skip record <see cref="SyncSkippedCourse"/> (FR-005, FR-006, VR-001 … VR-003).
/// </summary>
public sealed class SyncDataHealthInvariantTests
{
    private static readonly DateTimeOffset T = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    private static CourseWorkDetails ItemDetails(string title = "Test Item", decimal? maxPoints = 10m) =>
        new(title, T, T.AddDays(7), maxPoints, T.AddDays(-1), T.AddHours(-1));

    private static CourseWork AnItem() => CourseWork.Import(1, "800000000001", CourseWorkResource.CourseWork, ItemDetails());

    // ---- CourseWork (FR-001 … FR-003) ------------------------------------------------------------------------------

    /// <summary>Entity model §1: an imported item is in Classroom — it carries no mark.</summary>
    [Fact]
    public void AnImportedItem_IsNotMarked() => Assert.Null(AnItem().NotInClassroomSince);

    /// <summary>FR-001.2, FR-001.3: marking sets the instant, says the mark is new and changes no Google-sourced field.</summary>
    [Fact]
    public void Marking_SetsTheInstant_ReturnsTrue_AndLeavesTheMirror()
    {
        var item = AnItem();
        var before = (item.Title, item.ItemDate, item.DueAt, item.MaxPoints, item.CreationTime, item.UpdateTime, item.Kind);

        var isNew = item.MarkNotInClassroom(T.AddDays(3));

        Assert.True(isNew);
        Assert.Equal(T.AddDays(3), item.NotInClassroomSince);
        Assert.Equal(before, (item.Title, item.ItemDate, item.DueAt, item.MaxPoints, item.CreationTime, item.UpdateTime, item.Kind));
    }

    /// <summary>FR-002: marking an item already marked keeps the first instant and says the mark is not new.</summary>
    [Fact]
    public void MarkingAgain_KeepsTheFirstInstant_AndReturnsFalse()
    {
        var item = AnItem();
        item.MarkNotInClassroom(T.AddDays(3));

        var isNew = item.MarkNotInClassroom(T.AddDays(4));

        Assert.False(isNew);
        Assert.Equal(T.AddDays(3), item.NotInClassroomSince);
    }

    /// <summary>FR-003, entity model §1: an item Google returned is in Classroom by definition — the update clears the mark.</summary>
    [Fact]
    public void UpdatingFromGoogle_ClearsTheMark_AndAppliesTheData()
    {
        var item = AnItem();
        item.MarkNotInClassroom(T.AddDays(3));

        item.UpdateFrom(ItemDetails("Back again", 20m));

        Assert.Null(item.NotInClassroomSince);
        Assert.Equal("Back again", item.Title);
        Assert.Equal(20m, item.MaxPoints);
    }

    /// <summary>FR-003: after coming back, a later absence is a new mark with the new instant.</summary>
    [Fact]
    public void AnItemThatCameBack_IsMarkedAfreshWhenItGoesAgain()
    {
        var item = AnItem();
        item.MarkNotInClassroom(T.AddDays(3));
        item.UpdateFrom(ItemDetails());

        Assert.True(item.MarkNotInClassroom(T.AddDays(9)));
        Assert.Equal(T.AddDays(9), item.NotInClassroomSince);
    }

    // ---- Course (FR-008) -------------------------------------------------------------------------------------------

    private static CourseDetails CourseDetailsAt(DateTimeOffset? updateTime) =>
        new("Test Course", null, null, null, null, null, null, updateTime, null, null, null, null);

    /// <summary>FR-008.2: an imported course has no last-read instant.</summary>
    [Fact]
    public void AnImportedCourse_HasNoLastReadInstant() =>
        Assert.Null(Course.Import("100000000001", CourseState.Active, CourseDetailsAt(T)).LastCompleteReadAt);

    /// <summary>
    /// FR-008.1, entity model §2: recording a complete read stores the run instant as given, beside the listed updateTime
    /// it remembered before.
    /// </summary>
    [Fact]
    public void RecordingACompleteRead_StoresTheRunInstant_AndTheListedUpdateTime()
    {
        var course = Course.Import("100000000001", CourseState.Active, CourseDetailsAt(T.AddDays(-2)));

        course.RecordCompleteRead(T.AddHours(3));

        Assert.Equal(T.AddHours(3), course.LastCompleteReadAt);
        Assert.Equal(T.AddDays(-2), course.CompleteReadUpdateTime);
    }

    /// <summary>Entity model §2: refreshing the mirror leaves the last-read instant.</summary>
    [Fact]
    public void UpdatingFromTheListing_LeavesTheLastReadInstant()
    {
        var course = Course.Import("100000000001", CourseState.Active, CourseDetailsAt(T));
        course.RecordCompleteRead(T.AddHours(3));

        course.UpdateFrom(CourseState.Archived, CourseDetailsAt(T.AddDays(1)));

        Assert.Equal(T.AddHours(3), course.LastCompleteReadAt);
    }

    // ---- SyncSkippedCourse (FR-005, FR-006, VR-001 … VR-003) -------------------------------------------------------

    /// <summary>FR-005.2: a first skip holds the id, the name, the reason and the instant as given.</summary>
    [Fact]
    public void Recording_HoldsWhatItWasGiven()
    {
        var record = SyncSkippedCourse.Record("100000000001", "Test Course", CourseSkipReason.UnknownCourseState, T);

        Assert.Equal("100000000001", record.CourseGoogleId);
        Assert.Equal("Test Course", record.CourseName);
        Assert.Equal(CourseSkipReason.UnknownCourseState, record.Reason);
        Assert.Equal(T, record.SkippedSince);
    }

    /// <summary>VR-001, entity model §3: a blank Google id is refused — a record could never be found again.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankGoogleId_IsRefused(string googleId) =>
        Assert.ThrowsAny<ArgumentException>(() => SyncSkippedCourse.Record(googleId, "Test Course", CourseSkipReason.ValueOutOfRange, T));

    /// <summary>VR-001: the id is trimmed and cut to <see cref="Course.MaxGoogleIdLength"/>, as <see cref="Course.Import"/> cuts it.</summary>
    [Fact]
    public void TheGoogleId_IsTrimmedAndCut()
    {
        var longId = new string('7', Course.MaxGoogleIdLength + 5);

        Assert.Equal("100000000001", SyncSkippedCourse.Record("  100000000001 ", null, CourseSkipReason.ValueOutOfRange, T).CourseGoogleId);
        Assert.Equal(longId[..Course.MaxGoogleIdLength], SyncSkippedCourse.Record(longId, null, CourseSkipReason.ValueOutOfRange, T).CourseGoogleId);
    }

    /// <summary>VR-002: the name is trimmed and cut to <see cref="Course.MaxNameLength"/>, never refused; blank is no name.</summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Padded  ", "Padded")]
    public void TheName_IsTrimmed_AndBlankIsNoName(string? given, string? stored) =>
        Assert.Equal(stored, SyncSkippedCourse.Record("100000000001", given, CourseSkipReason.ValueOutOfRange, T).CourseName);

    /// <summary>VR-002: a name beyond the bound is cut, not refused.</summary>
    [Fact]
    public void ALongName_IsCut()
    {
        var name = new string('n', Course.MaxNameLength + 10);

        Assert.Equal(name[..Course.MaxNameLength], SyncSkippedCourse.Record("100000000001", name, CourseSkipReason.ValueOutOfRange, T).CourseName);
    }

    /// <summary>VR-003, entity model §3: an undefined reason is refused, as <c>SyncState.FailRun</c> refuses one.</summary>
    [Fact]
    public void AnUndefinedReason_IsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SyncSkippedCourse.Record("100000000001", null, (CourseSkipReason)99, T));
        var record = SyncSkippedCourse.Record("100000000001", null, CourseSkipReason.ValueOutOfRange, T);
        Assert.Throws<ArgumentOutOfRangeException>(() => record.SkipAgain(null, (CourseSkipReason)99));
    }

    /// <summary>FR-006, I-3: a repeated skip takes this run's name and reason and keeps the instant of the first.</summary>
    [Fact]
    public void SkippingAgain_TakesTheNameAndReason_AndKeepsTheInstant()
    {
        var record = SyncSkippedCourse.Record("100000000001", "First", CourseSkipReason.UnknownCourseState, T);

        record.SkipAgain("  Second  ", CourseSkipReason.ValueOutOfRange);

        Assert.Equal("Second", record.CourseName);
        Assert.Equal(CourseSkipReason.ValueOutOfRange, record.Reason);
        Assert.Equal(T, record.SkippedSince);
        Assert.Equal("100000000001", record.CourseGoogleId);
    }

    /// <summary>FR-006, VR-002: a repeated skip with a blank name stores no name.</summary>
    [Fact]
    public void SkippingAgain_WithABlankName_StoresNoName()
    {
        var record = SyncSkippedCourse.Record("100000000001", "First", CourseSkipReason.UnknownCourseState, T);

        record.SkipAgain("   ", CourseSkipReason.UnknownCourseState);

        Assert.Null(record.CourseName);
    }
}
