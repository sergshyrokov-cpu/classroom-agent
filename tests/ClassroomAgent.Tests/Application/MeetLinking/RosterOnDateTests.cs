using ClassroomAgent.Application.MeetLinking;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.MeetLinking;

/// <summary>
/// US-032 spec FR-002, I-1 (BR-051): a membership covers date D when it was first seen on or before D and is still on
/// the roster or was last seen on or after D — both as whole dates in the school's time zone (TC-8: Kyiv, UTC+3 in
/// September).
/// </summary>
public sealed class RosterOnDateTests
{
    private static readonly TimeZoneInfo Kyiv = JournalTestData.Kyiv;

    private static readonly DateOnly D = new(2026, 9, 11);

    /// <summary>Noon in Kyiv on <paramref name="day"/> of September 2026, as UTC.</summary>
    private static DateTimeOffset Noon(int day) => new(2026, 9, day, 9, 0, 0, TimeSpan.Zero);

    private static RosterMembership Membership(DateTimeOffset firstSeen, DateTimeOffset lastSeen, bool onRoster) =>
        new(1, ClassroomRole.Student, "student1@school-one.example.test", firstSeen, lastSeen, onRoster);

    [Fact]
    public void OnTheRoster_FirstSeenBefore_Covers() =>
        Assert.True(RosterOnDate.Covers(Membership(Noon(1), Noon(9), onRoster: true), D, Kyiv));

    [Fact]
    public void OnTheRoster_FirstSeenOnTheDay_Covers() =>
        Assert.True(RosterOnDate.Covers(Membership(Noon(11), Noon(11), onRoster: true), D, Kyiv));

    [Fact]
    public void OnTheRoster_FirstSeenTheDayAfter_DoesNotCover() =>
        Assert.False(RosterOnDate.Covers(Membership(Noon(12), Noon(12), onRoster: true), D, Kyiv));

    [Fact]
    public void OffTheRoster_LastSeenOnTheDay_Covers() =>
        Assert.True(RosterOnDate.Covers(Membership(Noon(1), Noon(11), onRoster: false), D, Kyiv));

    [Fact]
    public void OffTheRoster_LastSeenAfter_Covers() =>
        Assert.True(RosterOnDate.Covers(Membership(Noon(1), Noon(20), onRoster: false), D, Kyiv));

    [Fact]
    public void OffTheRoster_LastSeenTheDayBefore_DoesNotCover() =>
        Assert.False(RosterOnDate.Covers(Membership(Noon(1), Noon(10), onRoster: false), D, Kyiv));

    /// <summary>On the roster, "last seen" is irrelevant: a membership still on the roster covers every later day.</summary>
    [Fact]
    public void OnTheRoster_LastSeenLongBefore_StillCovers() =>
        Assert.True(RosterOnDate.Covers(Membership(Noon(1), Noon(2), onRoster: true), D, Kyiv));

    /// <summary>
    /// TC-8: 21:30 UTC on 10 September is 00:30 on 11 September in Kyiv. First seen then, the membership covers the 11th —
    /// a UTC date would say the 10th and wrongly cover the 10th too.
    /// </summary>
    [Fact]
    public void FirstSeenJustAfterLocalMidnight_CoversThatLocalDay_NotTheUtcDay()
    {
        var firstSeen = new DateTimeOffset(2026, 9, 10, 21, 30, 0, TimeSpan.Zero);
        var membership = Membership(firstSeen, firstSeen, onRoster: true);

        Assert.True(RosterOnDate.Covers(membership, new DateOnly(2026, 9, 11), Kyiv));
        Assert.False(RosterOnDate.Covers(membership, new DateOnly(2026, 9, 10), Kyiv));
    }

    /// <summary>TC-8: last seen at 21:30 UTC on 11 September is 00:30 on the 12th in Kyiv — it still covers the 12th.</summary>
    [Fact]
    public void LastSeenJustAfterLocalMidnight_CoversThatLocalDay()
    {
        var lastSeen = new DateTimeOffset(2026, 9, 11, 21, 30, 0, TimeSpan.Zero);
        var membership = Membership(Noon(1), lastSeen, onRoster: false);

        Assert.True(RosterOnDate.Covers(membership, new DateOnly(2026, 9, 12), Kyiv));
        Assert.False(RosterOnDate.Covers(membership, new DateOnly(2026, 9, 13), Kyiv));
    }
}
