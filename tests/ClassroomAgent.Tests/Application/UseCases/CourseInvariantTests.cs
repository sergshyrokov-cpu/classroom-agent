using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-014 VR-002, VR-003: what the three entities refuse and what they cut. A value longer than its bound is
/// <b>truncated, not refused</b> — a verbose course description must not make a run fail at the commit (the
/// <c>SyncState.LastError</c> precedent) — while the upsert keys are required, because a course or a person cannot
/// be stored without the identifier the upsert matches on (PC-3).
/// </summary>
/// <remarks>
/// The gap this class closes was recorded by TEST_WRITING rather than hidden: the bounds are asserted in the
/// schema tests, but the entity's cut-rather-refuse behaviour had no test while <c>Course.Import</c> threw
/// (test-generation report §9).
/// </remarks>
public sealed class CourseInvariantTests
{
    private static CourseDetails Details(
        string? name = null,
        string? description = null,
        string? section = null,
        string? ownerGoogleId = null) =>
        new(
            name ?? CourseTestData.CourseName(1),
            section,
            null,
            description,
            null,
            ownerGoogleId,
            null,
            null,
            null,
            null,
            null,
            null);

    /// <summary>VR-002: a description longer than its bound is cut to it, and the course is imported all the same.</summary>
    [Fact]
    public void AnOverlongDescription_IsCutNotRefused()
    {
        var verbose = new string('d', Course.MaxDescriptionLength + 500);

        var course = Course.Import(CourseTestData.CourseId(1), CourseState.Active, Details(description: verbose));

        Assert.Equal(Course.MaxDescriptionLength, course.Description!.Length);
        Assert.Equal(CourseTestData.CourseId(1), course.GoogleId);
    }

    /// <summary>VR-002: the name is bounded the same way, and the update half cuts exactly as the import does.</summary>
    [Fact]
    public void AnOverlongName_IsCutOnImportAndOnUpdate()
    {
        var long1 = new string('n', Course.MaxNameLength + 1);
        var long2 = new string('s', Course.MaxSectionLength + 1);

        var course = Course.Import(CourseTestData.CourseId(1), CourseState.Active, Details(name: long1));
        Assert.Equal(Course.MaxNameLength, course.Name.Length);

        course.UpdateFrom(CourseState.Archived, Details(name: long1, section: long2));

        Assert.Equal(Course.MaxNameLength, course.Name.Length);
        Assert.Equal(Course.MaxSectionLength, course.Section!.Length);
        Assert.Equal(CourseState.Archived, course.State);
    }

    /// <summary>VR-002: a value inside its bound is stored untouched — truncation is a safety net, not routine.</summary>
    [Fact]
    public void AValueInsideItsBound_IsStoredUntouched()
    {
        var course = Course.Import(
            CourseTestData.CourseId(1),
            CourseState.Active,
            Details(description: "A short description."));

        Assert.Equal("A short description.", course.Description);
    }

    /// <summary>VR-002, PC-3: the Google course id is required — it is the upsert key, so a course without one is not importable.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ACourseWithNoGoogleId_IsRefused(string googleId) =>
        Assert.Throws<ArgumentException>(() => Course.Import(googleId, CourseState.Active, Details()));

    /// <summary>db-design §3.3: a blank optional value is stored as absent, so "no owner" has one representation.</summary>
    [Fact]
    public void ABlankOptionalValue_BecomesAbsent()
    {
        var course = Course.Import(
            CourseTestData.CourseId(1),
            CourseState.Active,
            Details(ownerGoogleId: "   ", description: string.Empty));

        Assert.Null(course.OwnerGoogleId);
        Assert.Null(course.Description);
    }

    /// <summary>VR-003: the address is stored trimmed and lower-cased, so Epic 4's matching is not defeated by case.</summary>
    [Fact]
    public void AParticipantAddress_IsNormalised()
    {
        var participant = ClassroomParticipant.Import(
            CourseTestData.UserId(1),
            "  Person.One@School-One.Example.Test  ",
            "  Test Person 1  ");

        Assert.Equal("person.one@school-one.example.test", participant.Email);
        Assert.Equal("Test Person 1", participant.FullName);
    }

    /// <summary>VR-003, OD-006: a blank address and a blank name are stored as absent, not as empty strings.</summary>
    [Fact]
    public void ABlankAddressOrName_BecomesAbsent()
    {
        var participant = ClassroomParticipant.Import(CourseTestData.UserId(1), "   ", string.Empty);

        Assert.Null(participant.Email);
        Assert.Null(participant.FullName);
    }

    /// <summary>VR-003: an overlong name is cut, exactly as a course's strings are.</summary>
    [Fact]
    public void AnOverlongParticipantName_IsCut()
    {
        var participant = ClassroomParticipant.Import(
            CourseTestData.UserId(1),
            null,
            new string('p', ClassroomParticipant.MaxFullNameLength + 10));

        Assert.Equal(ClassroomParticipant.MaxFullNameLength, participant.FullName!.Length);
    }

    /// <summary>VR-003, PC-3: the Google <c>userId</c> is required — it is the person's only identity (OD-011).</summary>
    [Fact]
    public void AParticipantWithNoGoogleUserId_IsRefused() =>
        Assert.Throws<ArgumentException>(() => ClassroomParticipant.Import("  ", null, null));

    /// <summary>
    /// Entity model §4.2: a sighting earlier than the last one recorded is refused — time does not run backwards
    /// within a run sequence, and <c>ck_course_membership_seen_order</c> would refuse the row anyway.
    /// </summary>
    [Fact]
    public void ASightingEarlierThanTheLastOne_IsRefused()
    {
        var membership = Membership(out var seenAt);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            membership.SeenAgain(ClassroomRole.Student, seenAt - TimeSpan.FromMinutes(1)));
    }

    /// <summary>
    /// Entity model §4.2, FR-010: leaving the roster takes no instant at all, so the leaver's own expiry keeps
    /// counting from the last sighting (PC-11) instead of being postponed for ever.
    /// </summary>
    [Fact]
    public void LeavingTheRoster_KeepsTheLastSighting()
    {
        var membership = Membership(out var seenAt);

        membership.NotOnRoster();

        Assert.False(membership.OnRoster);
        Assert.Equal(seenAt, membership.LastSeenAt);
        Assert.Equal(seenAt, membership.FirstSeenAt);
    }

    private static CourseMembership Membership(out DateTimeOffset seenAt)
    {
        seenAt = InstallationTestHost.DefaultStart;
        return CourseMembership.FirstSeen(
            Course.Import(CourseTestData.CourseId(1), CourseState.Active, Details()),
            ClassroomParticipant.Import(CourseTestData.UserId(1), null, null),
            ClassroomRole.Student,
            seenAt);
    }
}
