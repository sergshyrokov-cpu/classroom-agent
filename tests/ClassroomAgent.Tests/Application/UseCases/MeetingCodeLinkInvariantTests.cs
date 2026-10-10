using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-032 entity model §1, db-design §2.2 (spec FR-001, FR-008 … FR-012, I-9): the two shapes of a link row, who made
/// it, when it may be confirmed, and the transitions between linked and marked — each renewing the concurrency stamp.
/// </summary>
public sealed class MeetingCodeLinkInvariantTests
{
    private const string Code = "abc-0001-xyz";

    private const long A = 101;

    private const long B = 102;

    private const long Person = 502;

    private const long Other = 501;

    private static readonly DateTimeOffset T0 = InstallationTestHost.DefaultStart;

    private static readonly DateTimeOffset T1 = T0 + TimeSpan.FromHours(1);

    [Fact]
    public void AnAutomaticLink_IsLinked_ByNobody_Unconfirmed_AndConfirmable()
    {
        var link = MeetingCodeLink.LinkAutomatically(Code, A, T0);

        Assert.Equal(MeetingCodeLinkState.Linked, link.State);
        Assert.Equal((Code, (long?)A, (bool?)true, (long?)null, (DateTimeOffset?)T0), (link.MeetingCode, link.CourseId, link.LinkedAutomatically, link.LinkedByAppUserId, link.LinkedAt));
        Assert.Null(link.ConfirmedByAppUserId);
        Assert.Null(link.ConfirmedAt);
        Assert.Null(link.MarkedByAppUserId);
        Assert.Null(link.MarkedAt);
        Assert.True(link.CanBeConfirmed);
        Assert.False(string.IsNullOrEmpty(link.ConcurrencyStamp));
    }

    /// <summary>I-9: a person's link records the person and is not offered for confirmation.</summary>
    [Fact]
    public void APersonsLink_RecordsThePerson_AndIsNotConfirmable()
    {
        var link = MeetingCodeLink.LinkByPerson(Code, A, Person, T0);

        Assert.Equal(MeetingCodeLinkState.Linked, link.State);
        Assert.Equal(((long?)A, (bool?)false, (long?)Person, (DateTimeOffset?)T0), (link.CourseId, link.LinkedAutomatically, link.LinkedByAppUserId, link.LinkedAt));
        Assert.False(link.CanBeConfirmed);
    }

    [Fact]
    public void AMark_HasNoCourse_AndRecordsWhoAndWhen()
    {
        var link = MeetingCodeLink.MarkNotACourse(Code, Person, T0);

        Assert.Equal(MeetingCodeLinkState.Marked, link.State);
        Assert.Null(link.CourseId);
        Assert.Null(link.LinkedAutomatically);
        Assert.Null(link.LinkedByAppUserId);
        Assert.Null(link.LinkedAt);
        Assert.Equal(((long?)Person, (DateTimeOffset?)T0), (link.MarkedByAppUserId, link.MarkedAt));
        Assert.False(link.CanBeConfirmed);
    }

    /// <summary>FR-009: confirming records who and when; nothing else changes; the link is no longer confirmable.</summary>
    [Fact]
    public void Confirming_RecordsWhoAndWhen_AndChangesNothingElse()
    {
        var link = MeetingCodeLink.LinkAutomatically(Code, A, T0);
        var stamp = link.ConcurrencyStamp;

        link.Confirm(Person, T1);

        Assert.Equal(((long?)Person, (DateTimeOffset?)T1), (link.ConfirmedByAppUserId, link.ConfirmedAt));
        Assert.Equal(((long?)A, (bool?)true, (long?)null, (DateTimeOffset?)T0), (link.CourseId, link.LinkedAutomatically, link.LinkedByAppUserId, link.LinkedAt));
        Assert.False(link.CanBeConfirmed);
        Assert.NotEqual(stamp, link.ConcurrencyStamp);
    }

    /// <summary>FR-010, I-9: re-linking makes it a person's link to the new course and clears the confirmation.</summary>
    [Fact]
    public void Relinking_MovesToTheCourse_ByThePerson_AndClearsTheConfirmation()
    {
        var link = MeetingCodeLink.LinkAutomatically(Code, A, T0);
        link.Confirm(Other, T0);
        var stamp = link.ConcurrencyStamp;

        link.Relink(B, Person, T1);

        Assert.Equal(((long?)B, (bool?)false, (long?)Person, (DateTimeOffset?)T1), (link.CourseId, link.LinkedAutomatically, link.LinkedByAppUserId, link.LinkedAt));
        Assert.Null(link.ConfirmedByAppUserId);
        Assert.Null(link.ConfirmedAt);
        Assert.NotEqual(stamp, link.ConcurrencyStamp);
    }

    /// <summary>FR-011: marking a confirmed automatic link clears course, maker and confirmation.</summary>
    [Fact]
    public void MarkingALink_ClearsCourseMakerAndConfirmation()
    {
        var link = MeetingCodeLink.LinkAutomatically(Code, A, T0);
        link.Confirm(Other, T0);
        var stamp = link.ConcurrencyStamp;

        link.Mark(Person, T1);

        Assert.Equal(MeetingCodeLinkState.Marked, link.State);
        Assert.Null(link.CourseId);
        Assert.Null(link.LinkedAutomatically);
        Assert.Null(link.LinkedByAppUserId);
        Assert.Null(link.LinkedAt);
        Assert.Null(link.ConfirmedByAppUserId);
        Assert.Null(link.ConfirmedAt);
        Assert.Equal(((long?)Person, (DateTimeOffset?)T1), (link.MarkedByAppUserId, link.MarkedAt));
        Assert.NotEqual(stamp, link.ConcurrencyStamp);
    }

    /// <summary>FR-012: picking a course for a marked code removes the mark and makes a person's unconfirmed link.</summary>
    [Fact]
    public void LinkingFromAMark_RemovesTheMark()
    {
        var link = MeetingCodeLink.MarkNotACourse(Code, Other, T0);
        var stamp = link.ConcurrencyStamp;

        link.LinkFromMark(A, Person, T1);

        Assert.Equal(MeetingCodeLinkState.Linked, link.State);
        Assert.Equal(((long?)A, (bool?)false, (long?)Person, (DateTimeOffset?)T1), (link.CourseId, link.LinkedAutomatically, link.LinkedByAppUserId, link.LinkedAt));
        Assert.Null(link.MarkedByAppUserId);
        Assert.Null(link.MarkedAt);
        Assert.NotEqual(stamp, link.ConcurrencyStamp);
    }

    // ---------------- Guards (AD-9: the use case checks first, so these are defects) ----------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankCode_IsRejected(string code)
    {
        Assert.Throws<ArgumentException>(() => MeetingCodeLink.LinkAutomatically(code, A, T0));
        Assert.Throws<ArgumentException>(() => MeetingCodeLink.LinkByPerson(code, A, Person, T0));
        Assert.Throws<ArgumentException>(() => MeetingCodeLink.MarkNotACourse(code, Person, T0));
    }

    [Fact]
    public void ACodeLongerThanSixtyFour_IsRejected() =>
        Assert.Throws<ArgumentException>(() => MeetingCodeLink.LinkAutomatically(new string('a', 65), A, T0));

    [Fact]
    public void ACodeOfSixtyFour_IsAccepted() =>
        Assert.Equal(64, MeetingCodeLink.LinkAutomatically(new string('a', 64), A, T0).MeetingCode.Length);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ANonPositiveId_IsRejected(long id)
    {
        Assert.ThrowsAny<ArgumentException>(() => MeetingCodeLink.LinkAutomatically(Code, id, T0));
        Assert.ThrowsAny<ArgumentException>(() => MeetingCodeLink.LinkByPerson(Code, A, id, T0));
        Assert.ThrowsAny<ArgumentException>(() => MeetingCodeLink.MarkNotACourse(Code, id, T0));
    }

    [Fact]
    public void ConfirmingAPersonsLink_IsADefect() =>
        Assert.Throws<InvalidOperationException>(() => MeetingCodeLink.LinkByPerson(Code, A, Person, T0).Confirm(Person, T1));

    [Fact]
    public void ConfirmingTwice_IsADefect()
    {
        var link = MeetingCodeLink.LinkAutomatically(Code, A, T0);
        link.Confirm(Person, T1);

        Assert.Throws<InvalidOperationException>(() => link.Confirm(Person, T1));
    }

    [Fact]
    public void RelinkingToTheSameCourse_IsADefect() =>
        Assert.Throws<InvalidOperationException>(() => MeetingCodeLink.LinkAutomatically(Code, A, T0).Relink(A, Person, T1));

    [Fact]
    public void RelinkingAMark_IsADefect() =>
        Assert.Throws<InvalidOperationException>(() => MeetingCodeLink.MarkNotACourse(Code, Person, T0).Relink(A, Person, T1));

    [Fact]
    public void MarkingAMark_IsADefect() =>
        Assert.Throws<InvalidOperationException>(() => MeetingCodeLink.MarkNotACourse(Code, Person, T0).Mark(Person, T1));

    [Fact]
    public void LinkingFromAMarkWhenLinked_IsADefect() =>
        Assert.Throws<InvalidOperationException>(() => MeetingCodeLink.LinkAutomatically(Code, A, T0).LinkFromMark(B, Person, T1));

    [Fact]
    public void ConfirmingAMark_IsADefect() =>
        Assert.Throws<InvalidOperationException>(() => MeetingCodeLink.MarkNotACourse(Code, Person, T0).Confirm(Person, T1));
}
