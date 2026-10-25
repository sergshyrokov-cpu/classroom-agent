using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-056 spec FR-001 (db-design §1, entity model §2): a membership the course's first load recorded — first seen no
/// later than the course row was stamped — starts at the course's Google <c>creationTime</c>, or at its first-seen
/// instant when Google gave none; any later membership starts at its first-seen instant.
/// </summary>
public sealed class MembershipStartTests
{
    private static readonly DateTimeOffset CreationTime = new(2025, 9, 1, 6, 0, 0, TimeSpan.Zero);

    /// <summary>The first-loading run's <c>observedAt</c>.</summary>
    private static readonly DateTimeOffset FirstRun = new(2026, 10, 22, 7, 0, 0, TimeSpan.Zero);

    /// <summary>The course row's <c>created_at</c>: stamped at save time, later in the same run.</summary>
    private static readonly DateTimeOffset CourseSaved = FirstRun.AddSeconds(5);

    private static readonly DateTimeOffset LaterRun = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    /// <summary>FR-001 a: seen in the first load, the course has a creationTime.</summary>
    [Fact]
    public void AFirstLoadMembership_StartsAtTheCourseCreationTime()
    {
        Assert.Equal(CreationTime, MembershipStart.Of(FirstRun, CourseSaved, CreationTime));
    }

    /// <summary>FR-001 b: seen in the first load, no creationTime — the first-loading run's instant.</summary>
    [Fact]
    public void AFirstLoadMembership_WithoutCreationTime_StartsAtTheFirstLoadingRun()
    {
        Assert.Equal(FirstRun, MembershipStart.Of(FirstRun, CourseSaved, null));
    }

    /// <summary>FR-001 c: a later arrival starts when it was first seen, creationTime notwithstanding.</summary>
    [Fact]
    public void ALaterArrival_StartsAtItsFirstSeenInstant()
    {
        Assert.Equal(LaterRun, MembershipStart.Of(LaterRun, CourseSaved, CreationTime));
    }

    /// <summary>FR-001 c without creationTime: still the first-seen instant.</summary>
    [Fact]
    public void ALaterArrival_WithoutCreationTime_StartsAtItsFirstSeenInstant()
    {
        Assert.Equal(LaterRun, MembershipStart.Of(LaterRun, CourseSaved, null));
    }

    /// <summary>db-design §1 boundary: first seen at exactly the course's save instant counts as the first load (≤).</summary>
    [Fact]
    public void FirstSeenAtTheCourseSaveInstant_CountsAsTheFirstLoad()
    {
        Assert.Equal(CreationTime, MembershipStart.Of(CourseSaved, CourseSaved, CreationTime));
    }

    /// <summary>db-design §1 boundary: one tick after the course's save instant is a later run.</summary>
    [Fact]
    public void FirstSeenOneTickAfterTheCourseSaveInstant_IsALaterArrival()
    {
        var justAfter = CourseSaved.AddTicks(1);

        Assert.Equal(justAfter, MembershipStart.Of(justAfter, CourseSaved, CreationTime));
    }

    /// <summary>FR-001 compares instants, not offsets: the same instant written in another offset is the same membership.</summary>
    [Fact]
    public void TheComparison_IsByInstant_NotByOffset()
    {
        var sameInstantInKyiv = CourseSaved.ToOffset(TimeSpan.FromHours(3));

        Assert.Equal(CreationTime, MembershipStart.Of(sameInstantInKyiv, CourseSaved, CreationTime));
    }
}
