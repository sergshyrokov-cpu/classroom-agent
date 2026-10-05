using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-042 AC-001 (spec FR-001, VR-003; db-design §2, D-3; entity model §1.2): a participant's surname and given name
/// — normalised exactly as the full name, taken from the roster entry by the synchronization on insert and replaced on
/// every later run, absent when the profile has none.
/// </summary>
public sealed class ParticipantNamePartsTests
{
    private const string Email = "olena.t@school-one.example.test";

    private static RosterEntry Entry(string? surname, string? givenName, int ordinal = 1) =>
        new(CourseTestData.UserId(ordinal), Email, "Olena Testova", surname, givenName);

    // ---------- The entity

    /// <summary>VR-003, D-3: trimmed; blank becomes none; case and alphabet untouched.</summary>
    [Theory]
    [InlineData("  Тестова  ", "Тестова")]
    [InlineData("TESTOVA", "TESTOVA")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void ImportNormalisesBothParts_AsTheFullName(string? raw, string? stored)
    {
        var participant = ClassroomParticipant.Import(CourseTestData.UserId(1), Email, "Olena Testova", raw, raw);

        Assert.Equal(stored, participant.Surname);
        Assert.Equal(stored, participant.GivenName);
        Assert.Equal("Olena Testova", participant.FullName);
    }

    /// <summary>D-2: each part is cut to 750 characters, as the full name; exactly 750 is kept whole.</summary>
    [Fact]
    public void BothParts_AreCutToTheirBound()
    {
        var exact = new string('а', ClassroomParticipant.MaxSurnameLength);
        var longer = new string('б', ClassroomParticipant.MaxGivenNameLength + 5);

        var participant = ClassroomParticipant.Import(CourseTestData.UserId(1), Email, null, exact, longer);

        Assert.Equal(750, ClassroomParticipant.MaxSurnameLength);
        Assert.Equal(750, ClassroomParticipant.MaxGivenNameLength);
        Assert.Equal(exact, participant.Surname);
        Assert.Equal(longer[..ClassroomParticipant.MaxGivenNameLength], participant.GivenName);
    }

    /// <summary>AC-001: an update replaces all four values; a part the profile no longer has becomes none.</summary>
    [Fact]
    public void UpdateFrom_ReplacesEveryValue()
    {
        var participant = ClassroomParticipant.Import(CourseTestData.UserId(1), Email, "Olena Testova", "Тестова", "Олена");

        participant.UpdateFrom("new.address@school-one.example.test", "Olena Novak", "Новак", null);

        Assert.Equal("new.address@school-one.example.test", participant.Email);
        Assert.Equal("Olena Novak", participant.FullName);
        Assert.Equal("Новак", participant.Surname);
        Assert.Null(participant.GivenName);
    }

    // ---------- The synchronization (Application, ports substituted — TC-1, TC-4)

    /// <summary>AC-001: a new roster entry's surname and given name are stored with the participant.</summary>
    [Fact]
    public async Task ARosterEntry_IsImportedWithItsNameParts()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom.WithCourse(
            CourseTestData.CourseId(1),
            CourseTestData.CourseName(1),
            teachers: [Entry("Шевчук", "Марія", ordinal: 1)],
            students: [Entry("Тестова", "Олена", ordinal: 2), Entry(null, null, ordinal: 3)]);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var teacher = world.Participants.Stored[CourseTestData.UserId(1)];
        Assert.Equal(("Шевчук", "Марія"), (teacher.Surname, teacher.GivenName));
        var student = world.Participants.Stored[CourseTestData.UserId(2)];
        Assert.Equal(("Тестова", "Олена"), (student.Surname, student.GivenName));
        var nameless = world.Participants.Stored[CourseTestData.UserId(3)];
        Assert.Null(nameless.Surname);
        Assert.Null(nameless.GivenName);
        Assert.Equal("Olena Testova", nameless.FullName);
    }

    /// <summary>AC-001: the next run replaces a changed name and clears a removed part — no merge with the old values.</summary>
    [Fact]
    public async Task TheNextRun_ReplacesChangedAndRemovedParts()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom.WithCourse(
            CourseTestData.CourseId(1),
            CourseTestData.CourseName(1),
            students: [Entry("Тестова", "Олена", ordinal: 1), Entry("Бойко", "Тарас", ordinal: 2)]);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        world.Classroom.WithRoster(
            CourseTestData.CourseId(1),
            students: [Entry("Новак", "Олена", ordinal: 1), Entry(null, null, ordinal: 2)]);
        world.Time.Advance(SyncTestData.DefaultInterval);
        await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Equal(2, world.Participants.Added.Count);
        var renamed = world.Participants.Stored[CourseTestData.UserId(1)];
        Assert.Equal(("Новак", "Олена"), (renamed.Surname, renamed.GivenName));
        var cleared = world.Participants.Stored[CourseTestData.UserId(2)];
        Assert.Null(cleared.Surname);
        Assert.Null(cleared.GivenName);
    }
}
