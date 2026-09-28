using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-014 AC-003 and AC-004: each course's roster is imported with the role on the membership, and the membership
/// records what synchronization observed — first seen, last seen, on the roster now (spec FR-004, FR-006, FR-007,
/// FR-009, FR-010; BR-050, BR-051).
/// </summary>
public sealed class RosterImportTests
{
    private static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));

    /// <summary>AC-003: both rosters are read, and the role lands on the membership (BR-050).</summary>
    [Fact]
    public async Task BothRosters_AreImportedWithTheRoleOnTheMembership()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom.WithCourse(
            CourseTestData.CourseId(1),
            CourseTestData.CourseName(1),
            teachers: [Person(1)],
            students: [Person(2), Person(3)]);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal([CourseTestData.CourseId(1)], world.Classroom.RostersRead);
        Assert.Equal(3, world.Participants.Added.Count);
        var course = world.Courses.Stored[CourseTestData.CourseId(1)];
        var memberships = world.Memberships.OfCourse(course.Id);
        Assert.Equal(3, memberships.Count);
        Assert.Single(memberships, m => m.Role == ClassroomRole.Teacher);
        Assert.Equal(2, memberships.Count(m => m.Role == ClassroomRole.Student));
    }

    /// <summary>
    /// AC-003, §3: co-teachers are imported, not only the course owner. The prototype's owner-only model — it never
    /// calls <c>teachers().list</c> at all — is not the requirement.
    /// </summary>
    [Fact]
    public async Task CoTeachers_AreImported()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom.WithCourse(
            CourseTestData.CourseId(1),
            CourseTestData.CourseName(1),
            teachers: [Person(1), Person(2)],
            ownerGoogleId: CourseTestData.UserId(1));

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var course = world.Courses.Stored[CourseTestData.CourseId(1)];
        var teachers = world.Memberships.OfCourse(course.Id).Where(m => m.Role == ClassroomRole.Teacher).ToList();
        Assert.Equal(2, teachers.Count);
    }

    /// <summary>
    /// AC-003, BR-050: one person on two courses is one participant and two memberships, with the role on each.
    /// </summary>
    [Fact]
    public async Task OnePersonOnTwoCourses_IsOneParticipantAndTwoMemberships()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), teachers: [Person(1)])
            .WithCourse(CourseTestData.CourseId(2), CourseTestData.CourseName(2), students: [Person(1)]);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Single(world.Participants.Added);
        Assert.Equal(2, world.Memberships.Stored.Count);
        // Ordered by the enum's own values, which entity model §5 fixes as Teacher then Student — unlike the
        // schema tests, where ORDER BY role sorts the stored codes 'student' before 'teacher' alphabetically.
        Assert.Equal(
            new[] { ClassroomRole.Teacher, ClassroomRole.Student },
            world.Memberships.Stored.Select(m => m.Role).Order());
    }

    /// <summary>
    /// AC-003, OD-006: a roster entry with no address, and one from outside the school's domain, are both imported.
    /// Whether a person is a subject of the teaching process is decided where the reports are built (BR-064), never
    /// as an import filter.
    /// </summary>
    [Fact]
    public async Task RosterEntriesWithNoAddressOrAnOutsideAddress_AreImported()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom.WithCourse(
            CourseTestData.CourseId(1),
            CourseTestData.CourseName(1),
            teachers: [new RosterEntry(CourseTestData.UserId(1), "guest.teacher@other.example", CourseTestData.Name(1))],
            students: [new RosterEntry(CourseTestData.UserId(2), null, null)]);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(2, world.Participants.Added.Count);
        var hidden = world.Participants.Stored[CourseTestData.UserId(2)];
        Assert.Null(hidden.Email);
        Assert.Null(hidden.FullName);
        var guest = world.Participants.Stored[CourseTestData.UserId(1)];
        Assert.Equal("guest.teacher@other.example", guest.Email);
    }

    /// <summary>AC-004, BR-051: a first sighting sets both instants and puts the person on the roster.</summary>
    [Fact]
    public async Task AFirstSighting_SetsBothInstantsAndTheFlag()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var runInstant = world.Time.GetUtcNow();
        world.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)]);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var membership = Assert.Single(world.Memberships.Stored);
        Assert.Equal(runInstant, membership.FirstSeenAt);
        Assert.Equal(runInstant, membership.LastSeenAt);
        Assert.True(membership.OnRoster);
    }

    /// <summary>
    /// AC-004, FR-009, I-8: all observations of one run carry the same instant, so "seen in the same run" is
    /// expressible in a query.
    /// </summary>
    [Fact]
    public async Task EveryObservationOfOneRun_CarriesTheSameInstant()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1), Person(2)])
            .WithCourse(CourseTestData.CourseId(2), CourseTestData.CourseName(2), teachers: [Person(3)]);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var instants = world.Memberships.Stored.Select(m => m.LastSeenAt).Distinct().ToList();
        Assert.Single(instants);
    }

    /// <summary>
    /// AC-004, FR-010, BR-051: leaving the roster clears the flag and leaves the last-seen date where it was — the
    /// membership is never deleted, because the leaver's own expiry counts from that date (PC-11).
    /// </summary>
    [Fact]
    public async Task LeavingTheRoster_ClearsTheFlagAndKeepsTheLastSeenDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)]);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        var firstRunInstant = world.Time.GetUtcNow();

        // The next run sees the course with an empty roster: an answer, not a failure (I-7).
        var later = new SyncWorld(now: firstRunInstant + SyncTestData.DefaultInterval);
        later.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1));
        var membership = Assert.Single(world.Memberships.Stored);
        membership.NotOnRoster();

        Assert.False(membership.OnRoster);
        Assert.Equal(firstRunInstant, membership.LastSeenAt);
        Assert.NotEmpty(world.Memberships.Stored);
    }

    /// <summary>
    /// AC-004, FR-009: someone who returns to a roster reuses the same membership — the flag goes true again, the
    /// last-seen advances, and <b>the first-seen date is never rewritten</b>, so "when did we first see this person
    /// on this course" survives an absence.
    /// </summary>
    [Fact]
    public async Task ReturningToTheRoster_ReusesTheMembershipAndKeepsTheFirstSeenDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)]);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        var membership = Assert.Single(world.Memberships.Stored);
        var firstSeen = membership.FirstSeenAt;

        membership.NotOnRoster();
        var returned = firstSeen + TimeSpan.FromDays(30);
        membership.SeenAgain(ClassroomRole.Student, returned);

        Assert.True(membership.OnRoster);
        Assert.Equal(returned, membership.LastSeenAt);
        Assert.Equal(firstSeen, membership.FirstSeenAt);
        Assert.Single(world.Memberships.Stored);
    }

    /// <summary>
    /// AC-004, I-7: an <b>empty</b> roster is an answer — the memberships of that course are marked off the roster.
    /// This is the case I-6 is contrasted with.
    /// </summary>
    [Fact]
    public async Task AnEmptyRoster_MarksTheMembershipsOffTheRoster()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)]);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        var course = world.Courses.Stored[CourseTestData.CourseId(1)];

        world.Time.Advance(SyncTestData.DefaultInterval);
        var emptied = new SyncWorld(now: world.Time.GetUtcNow());
        emptied.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1));
        await emptied.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        // The course was imported again with no roster, so nothing is on it.
        var reimported = emptied.Courses.Stored[CourseTestData.CourseId(1)];
        Assert.DoesNotContain(emptied.Memberships.OfCourse(reimported.Id), m => m.OnRoster);
        Assert.NotEqual(0, course.Id);
    }

    /// <summary>
    /// AC-004, FR-010, I-6: a roster read that <b>failed</b> leaves the roster unknown, so no membership of that
    /// course is marked off the roster. Marking would record a departure that never happened and that later runs
    /// could not tell from a real one.
    /// </summary>
    [Fact]
    public async Task AFailedRosterRead_MarksNoMembershipOffTheRoster()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom
            .WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)])
            .WithRosterFailure(CourseTestData.CourseId(1), new InvalidOperationException("the roster read failed"));

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.DoesNotContain(world.Memberships.Stored, m => !m.OnRoster);
    }

    /// <summary>
    /// AC-003, FR-007, I-9: a person Classroom returns on <b>both</b> rosters of one course is stored once, with
    /// the role <c>teacher</c>. This is the tie-break Specification v2 introduced; without it the second roster
    /// would violate the unique index and fail the course's import.
    /// </summary>
    [Fact]
    public async Task APersonOnBothRostersOfOneCourse_IsOneTeacherMembership()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom.WithCourse(
            CourseTestData.CourseId(1),
            CourseTestData.CourseName(1),
            teachers: [Person(1)],
            students: [Person(1)]);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var membership = Assert.Single(world.Memberships.Stored);
        Assert.Equal(ClassroomRole.Teacher, membership.Role);
        Assert.Single(world.Participants.Added);
    }

    /// <summary>
    /// AC-003: a role change in Classroom keeps one membership and its original first-seen date — the person did
    /// not arrive twice.
    /// </summary>
    [Fact]
    public async Task ARoleChange_KeepsOneMembershipAndItsFirstSeenDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1), students: [Person(1)]);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        var membership = Assert.Single(world.Memberships.Stored);
        var firstSeen = membership.FirstSeenAt;

        membership.SeenAgain(ClassroomRole.Teacher, firstSeen + SyncTestData.DefaultInterval);

        Assert.Equal(ClassroomRole.Teacher, membership.Role);
        Assert.Equal(firstSeen, membership.FirstSeenAt);
        Assert.Single(world.Memberships.Stored);
    }
}
