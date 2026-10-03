using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-037 spec FR-001, FR-002 and AC-014, AC-016: the one definition of "older than N" that synchronization and the
/// purge share — the cutoff, the strict boundary, and "latest of the dates, nulls ignored".
/// </summary>
public sealed class RetentionRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheCutoff_IsNowMinusNCalendarYears()
    {
        Assert.Equal(new DateTimeOffset(2021, 9, 17, 8, 0, 0, TimeSpan.Zero), RetentionRule.Cutoff(Now, 5));
    }

    /// <summary>A leap day minus whole years lands where <see cref="DateTimeOffset.AddYears"/> lands — the US-015 rule.</summary>
    [Fact]
    public void TheCutoff_FollowsCalendarYears_OnALeapDay()
    {
        var leapDay = new DateTimeOffset(2028, 2, 29, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(leapDay.AddYears(-1), RetentionRule.Cutoff(leapDay, 1));
    }

    /// <summary>AC-014: a date equal to the cutoff is kept — the comparison is strict (US-015 I-3).</summary>
    [Fact]
    public void ADateOnTheCutoff_IsNotExpired()
    {
        var cutoff = RetentionRule.Cutoff(Now, 5);

        Assert.False(RetentionRule.IsExpired(cutoff, cutoff));
    }

    /// <summary>AC-014: one tick earlier is expired.</summary>
    [Fact]
    public void ADateOneTickBeforeTheCutoff_IsExpired()
    {
        var cutoff = RetentionRule.Cutoff(Now, 5);

        Assert.True(RetentionRule.IsExpired(cutoff.AddTicks(-1), cutoff));
    }

    [Fact]
    public void ADateAfterTheCutoff_IsNotExpired()
    {
        var cutoff = RetentionRule.Cutoff(Now, 5);

        Assert.False(RetentionRule.IsExpired(cutoff.AddTicks(1), cutoff));
    }

    /// <summary>FR-002: the latest of the dates wins, wherever it is in the list.</summary>
    [Fact]
    public void TheLatestActivity_IsTheLatestDate()
    {
        var early = Now.AddYears(-7);
        var late = Now.AddYears(-1);

        Assert.Equal(late, RetentionRule.LatestActivity([early, late, Now.AddYears(-3)]));
        Assert.Equal(late, RetentionRule.LatestActivity([late, early]));
    }

    /// <summary>FR-002: null dates are ignored, not treated as "very old" or "now".</summary>
    [Fact]
    public void NullDates_AreIgnored()
    {
        var only = Now.AddYears(-6);

        Assert.Equal(only, RetentionRule.LatestActivity([null, only, null]));
    }

    /// <summary>FR-002, OD-006: with no date at all there is no activity — the caller decides the fallback.</summary>
    [Fact]
    public void WithNoDateAtAll_ThereIsNoActivity()
    {
        Assert.Null(RetentionRule.LatestActivity([null, null]));
        Assert.Null(RetentionRule.LatestActivity([]));
    }

    /// <summary>Offsets are compared as instants, so a non-UTC date is not misplaced (TC-8).</summary>
    [Fact]
    public void DatesWithOffsets_AreComparedAsInstants()
    {
        var kyiv = new DateTimeOffset(2026, 9, 17, 10, 30, 0, TimeSpan.FromHours(3));
        var utcEarlier = new DateTimeOffset(2026, 9, 17, 7, 0, 0, TimeSpan.Zero);

        Assert.Equal(kyiv, RetentionRule.LatestActivity([utcEarlier, kyiv]));
        Assert.False(RetentionRule.IsExpired(kyiv, kyiv.ToUniversalTime()));
    }
}
