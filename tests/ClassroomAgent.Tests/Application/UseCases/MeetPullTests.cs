using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.MeetTestData;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-031 AC-001 … AC-005 (spec FR-001, FR-003, FR-005, FR-006, FR-008, FR-009, I-2, I-4): what the Meet step of a run
/// stores. The run use case is the unit and both Google ports are substituted (TC-1, TC-4); every meeting, endpoint
/// and account is synthetic. Times are placed relative to <see cref="InstallationTestHost.DefaultStart"/>, inside the
/// first pull's window.
/// </summary>
public sealed class MeetPullTests
{
    /// <summary>The join of the earliest connection of the meetings below — two days before the run.</summary>
    private static readonly DateTimeOffset T0 = InstallationTestHost.DefaultStart - TimeSpan.FromDays(2);

    private static DateTimeOffset At(int minutes) => T0 + TimeSpan.FromMinutes(minutes);

    /// <summary>Three connections of conference 1: joins T0, T0+10m, T0+40m; leaves T0+60m, T0+50m, T0+70m.</summary>
    private static FakeMeetReportsReader SeedThreeConnections(FakeMeetReportsReader reader) =>
        reader.WithPage(
            Event(1, 1, At(60), 3600, Teacher(1), Teacher(1)),
            Event(1, 2, At(50), 2400, Teacher(1), Student(1)),
            Event(1, 3, At(70), 1800, Teacher(1), Student(2)));

    /// <summary>
    /// AC-001, FR-008: a meeting organized by a domain account is stored with its conference id, meeting code, organizer,
    /// start, end and one participation per endpoint; the run completes.
    /// </summary>
    [Fact]
    public async Task ADomainMeeting_IsStoredWithItsValues_AndOneParticipationPerEndpoint()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        SeedThreeConnections(world.Meet);

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.False(outcome.Failed);
        Assert.Equal(SyncRunStatus.Completed, world.States.Stored!.Status);
        var session = Assert.Single(world.MeetSessions.Stored.Values);
        Assert.Equal(ConferenceId(1), session.ConferenceId);
        Assert.Equal(MeetingCode(1), session.MeetingCode);
        Assert.Equal(Teacher(1), session.OrganizerEmail);
        Assert.Equal(At(0), session.StartedAt);
        Assert.Equal(At(70), session.EndedAt);
        Assert.Equal(
            new[] { EndpointId(1), EndpointId(2), EndpointId(3) },
            session.Participations.Select(p => p.EndpointId).OrderBy(id => id, StringComparer.Ordinal));
    }

    /// <summary>AC-001, FR-008, I-3: each participation's join is the event time minus the duration, and the duration is kept.</summary>
    [Fact]
    public async Task AParticipation_JoinsAtTheEventTimeMinusTheDuration()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        SeedThreeConnections(world.Meet);

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var byEndpoint = world.MeetSessions.AllParticipations.ToDictionary(p => p.EndpointId);
        Assert.Equal((At(0), 3600), (byEndpoint[EndpointId(1)].JoinedAt, byEndpoint[EndpointId(1)].DurationSeconds));
        Assert.Equal((At(10), 2400), (byEndpoint[EndpointId(2)].JoinedAt, byEndpoint[EndpointId(2)].DurationSeconds));
        Assert.Equal((At(40), 1800), (byEndpoint[EndpointId(3)].JoinedAt, byEndpoint[EndpointId(3)].DurationSeconds));
    }

    /// <summary>
    /// FR-001: the Meet step runs after the Classroom step, in the same run — when the Meet port is called the course of
    /// the Classroom step is already stored — and exactly once.
    /// </summary>
    [Fact]
    public async Task TheMeetStep_RunsOnceAfterTheClassroomStep()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Classroom.WithCourse(CourseTestData.CourseId(1), CourseTestData.CourseName(1));
        SeedThreeConnections(world.Meet);
        var coursesWhenMeetWasCalled = -1;
        world.Meet.OnCall = () => coursesWhenMeetWasCalled = world.Courses.Stored.Count;

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Single(world.Meet.Calls);
        Assert.Equal(1, coursesWhenMeetWasCalled);
        Assert.Equal(1, world.Classroom.CourseReads);
    }

    /// <summary>FR-003, BR-015: the Meet events are read as the school's technical account, never as a person.</summary>
    [Fact]
    public async Task TheMeetEvents_AreReadAsTheTechnicalAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var call = Assert.Single(world.Meet.Calls);
        Assert.Equal(AccessCheckTestData.TechnicalAccount, call.ImpersonationUser);
    }

    /// <summary>
    /// AC-002, FR-009: a later run that reads a late connection of a stored meeting adds it and recomputes start and end
    /// over all connections, old and new.
    /// </summary>
    [Fact]
    public async Task ALaterRun_AddsLateConnections_AndRecomputesStartAndEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Meet.WithPage(Event(1, 1, At(60), 3600, Teacher(1), Teacher(1)));
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        var session = world.MeetSessions.Stored[ConferenceId(1)];
        Assert.Equal((At(0), At(60)), (session.StartedAt, session.EndedAt));

        world.Time.Advance(SyncTestData.DefaultInterval);
        world.Meet.Reset().WithPage(
            Event(1, 1, At(60), 3600, Teacher(1), Teacher(1)),
            Event(1, 2, At(90), 6000, Teacher(1), Student(1)));
        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.False(outcome.Failed);
        session = Assert.Single(world.MeetSessions.Stored.Values);
        Assert.Equal(At(-10), session.StartedAt);
        Assert.Equal(At(90), session.EndedAt);
        Assert.Equal(2, session.Participations.Count);
    }

    /// <summary>
    /// AC-002, FR-009: an endpoint read again with other values is updated in place, and start and end follow it — the
    /// stored values are recomputed, not only widened.
    /// </summary>
    [Fact]
    public async Task AConnectionReadAgainWithOtherValues_IsUpdated_AndTheMeetingFollows()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Meet.WithPage(
            Event(1, 1, At(60), 3600, Teacher(1), Teacher(1)),
            Event(1, 2, At(30), 600, Teacher(1), Student(1)));
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        world.Time.Advance(SyncTestData.DefaultInterval);
        world.Meet.Reset().WithPage(Event(1, 1, At(80), 1200, Teacher(1), Teacher(1)));
        await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        var session = world.MeetSessions.Stored[ConferenceId(1)];
        var first = session.Participations.Single(p => p.EndpointId == EndpointId(1));
        Assert.Equal((At(60), 1200), (first.JoinedAt, first.DurationSeconds));
        Assert.Equal(At(20), session.StartedAt);
        Assert.Equal(At(80), session.EndedAt);
        Assert.Equal(2, session.Participations.Count);
    }

    /// <summary>AC-003, FR-009, PC-10: the same events read in two runs leave one meeting and one row per endpoint.</summary>
    [Fact]
    public async Task TheSameEventsReadTwice_LeaveOneMeetingAndOneRowPerEndpoint()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        SeedThreeConnections(world.Meet);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        world.Time.Advance(SyncTestData.DefaultInterval);
        await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Single(world.MeetSessions.Added);
        var session = Assert.Single(world.MeetSessions.Stored.Values);
        Assert.Equal(3, session.Participations.Count);
        Assert.Equal(3, session.Participations.Select(p => p.EndpointId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal((At(0), At(70)), (session.StartedAt, session.EndedAt));
    }

    /// <summary>AC-003, PC-10: one endpoint reported twice in one run is one row, not two.</summary>
    [Fact]
    public async Task OneEndpointTwiceInOneRun_IsOneRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Meet
            .WithPage(Event(1, 1, At(60), 3600, Teacher(1), Teacher(1)))
            .WithPage(Event(1, 1, At(60), 3600, Teacher(1), Teacher(1)));

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.False(outcome.Failed);
        var session = Assert.Single(world.MeetSessions.Stored.Values);
        Assert.Single(session.Participations);
    }

    /// <summary>
    /// AC-004, FR-005, FR-006, OD-004, OD-005: a meeting whose every event names another domain, a subdomain, a malformed
    /// address or no organizer is not stored, nor any of its connections; the events count as not of the school and the
    /// run completes. The control stores the same meeting with a domain organizer.
    /// </summary>
    [Theory]
    [InlineData("teacher1@" + OtherDomain)]
    [InlineData("teacher1@" + Subdomain)]
    [InlineData("teacher1-at-school")]
    [InlineData(null)]
    public async Task AMeetingNotOrganizedFromTheDomain_IsNotStored(string? organizer)
    {
        var ct = TestContext.Current.CancellationToken;
        var control = new SyncWorld();
        control.Meet.WithPage(Event(1, 1, At(60), 3600, Teacher(1), Student(1)));
        await control.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        Assert.Single(control.MeetSessions.Stored);

        var world = new SyncWorld();
        world.Meet.WithPage(Event(1, 1, At(60), 3600, organizer, Student(1)));

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.False(outcome.Failed);
        Assert.Equal(SyncRunStatus.Completed, world.States.Stored!.Status);
        Assert.Empty(world.MeetSessions.Stored);
        Assert.Empty(world.MeetSessions.Added);
        Assert.Equal(1, outcome.Meet!.NotOfTheSchool);
        Assert.Equal(0, outcome.Meet.SkippedTotal);
    }

    /// <summary>
    /// FR-005, I-4: the decision is per conference — one event of it naming a domain organizer stores the meeting with
    /// every connection read in the run, including the events that named none, and that email is the organizer.
    /// </summary>
    [Fact]
    public async Task OneDomainOrganizerAmongTheEventsOfAConference_StoresItWithAllItsConnections()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Meet
            .WithPage(Event(1, 1, At(50), 3000, null, Student(1)))
            .WithPage(Event(1, 2, At(60), 3600, Teacher(1), Teacher(1)));

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var session = Assert.Single(world.MeetSessions.Stored.Values);
        Assert.Equal(Teacher(1), session.OrganizerEmail);
        Assert.Equal(2, session.Participations.Count);
    }

    /// <summary>
    /// FR-005, I-4: once stored, a conference takes every later connection whatever its event says of the organizer, and
    /// its meeting code and organizer keep their stored values.
    /// </summary>
    [Fact]
    public async Task AStoredConference_TakesLaterConnections_AndKeepsItsCodeAndOrganizer()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Meet.WithPage(Event(1, 1, At(60), 3600, Teacher(1), Teacher(1)));
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        world.Time.Advance(SyncTestData.DefaultInterval);
        world.Meet.Reset().WithPage(
            Event(1, 2, At(65), 3600, null, Student(1)),
            Event(1, 3, At(66), 3600, "someone@" + OtherDomain, Student(2), meetingCode: "zzz-9999-zzz"));
        await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        var session = Assert.Single(world.MeetSessions.Stored.Values);
        Assert.Equal(3, session.Participations.Count);
        Assert.Equal(Teacher(1), session.OrganizerEmail);
        Assert.Equal(MeetingCode(1), session.MeetingCode);
    }

    /// <summary>
    /// FR-005: a conference skipped as not of the school in one run is stored by a later run that reads a domain
    /// organizer for it — with the connections that run reads.
    /// </summary>
    [Fact]
    public async Task AConferenceSkippedEarlier_IsStoredWhenALaterRunReadsADomainOrganizer()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Meet.WithPage(Event(1, 1, At(50), 3000, null, Student(1)));
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        Assert.Empty(world.MeetSessions.Stored);

        world.Time.Advance(SyncTestData.DefaultInterval);
        world.Meet.Reset().WithPage(Event(1, 2, At(60), 3600, Teacher(1), Teacher(1)));
        await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        var session = Assert.Single(world.MeetSessions.Stored.Values);
        Assert.Equal(EndpointId(2), Assert.Single(session.Participations).EndpointId);
    }

    /// <summary>FR-006, AC-014: the organizer is matched case-insensitively and stored as Google returned it.</summary>
    [Fact]
    public async Task AnOrganizerInOtherCase_IsADomainAccount_AndIsStoredAsReturned()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var organizer = "Teacher1@" + SchoolDomain.ToUpperInvariant();
        world.Meet.WithPage(Event(1, 1, At(60), 3600, organizer, Student(1)));

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(organizer, Assert.Single(world.MeetSessions.Stored.Values).OrganizerEmail);
    }

    /// <summary>
    /// AC-005, FR-006, FR-008: of a domain account, a subdomain account, an external guest, a connection without an
    /// account and a phone dial-in, only the domain account's connection holds an email; the others are "other
    /// participants" with no email.
    /// </summary>
    [Fact]
    public async Task OnlyADomainAccountsConnection_HoldsAnEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Meet.WithPage(
            Event(1, 1, At(60), 3600, Teacher(1), Student(1)),
            Event(1, 2, At(60), 3600, Teacher(1), "student2@" + Subdomain),
            Event(1, 3, At(60), 3600, Teacher(1), "guest@" + OtherDomain),
            Event(1, 4, At(60), 3600, Teacher(1), null),
            Event(1, 5, At(60), 3600, Teacher(1), Student(3), identifierType: PhoneIdentifierType));

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var byEndpoint = world.MeetSessions.AllParticipations.ToDictionary(p => p.EndpointId);
        Assert.Equal(5, byEndpoint.Count);
        Assert.Equal(Student(1), byEndpoint[EndpointId(1)].Email);
        Assert.False(byEndpoint[EndpointId(1)].IsOtherParticipant);
        foreach (var other in new[] { 2, 3, 4, 5 })
        {
            Assert.Null(byEndpoint[EndpointId(other)].Email);
            Assert.True(byEndpoint[EndpointId(other)].IsOtherParticipant);
        }
    }

    /// <summary>FR-009: one conference's connections spread over two pages make one meeting, start and end over both.</summary>
    [Fact]
    public async Task AConferenceSpreadOverTwoPages_IsOneMeetingOverBoth()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Meet
            .WithPage(Event(1, 1, At(60), 3600, Teacher(1), Teacher(1)))
            .WithPage(Event(1, 2, At(120), 600, Teacher(1), Student(1)));

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var session = Assert.Single(world.MeetSessions.Stored.Values);
        Assert.Equal((At(0), At(120)), (session.StartedAt, session.EndedAt));
        Assert.Equal(2, session.Participations.Count);
    }

    /// <summary>PC-11, FR-005: synchronization never deletes a stored meeting — a later run that reads nothing keeps it.</summary>
    [Fact]
    public async Task ALaterRunReadingNothing_KeepsTheStoredMeeting()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        SeedThreeConnections(world.Meet);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        Assert.Single(world.MeetSessions.Stored);

        world.Time.Advance(SyncTestData.DefaultInterval);
        world.Meet.Reset();
        await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct);

        Assert.Equal(3, Assert.Single(world.MeetSessions.Stored.Values).Participations.Count);
    }

    /// <summary>
    /// FR-012: the run outcome carries the Meet step's counts for the host's log line — events read, meetings and
    /// connections stored new and updated, events of meetings not of the school.
    /// </summary>
    [Fact]
    public async Task TheOutcome_CarriesTheMeetStepCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        world.Meet.WithPage(
            Event(1, 1, At(60), 3600, Teacher(1), Teacher(1)),
            Event(1, 2, At(60), 3600, Teacher(1), Student(1)),
            Event(2, 3, At(60), 3600, "someone@" + OtherDomain, Student(2)));

        var first = (await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct)).Meet!;

        Assert.Equal(3, first.EventsRead);
        Assert.Equal((1, 0), (first.SessionsAdded, first.SessionsUpdated));
        Assert.Equal((2, 0), (first.ParticipationsAdded, first.ParticipationsUpdated));
        Assert.Equal(1, first.NotOfTheSchool);
        Assert.Equal(0, first.SkippedTotal);

        world.Time.Advance(SyncTestData.DefaultInterval);
        world.Meet.Reset().WithPage(
            Event(1, 1, At(60), 3600, Teacher(1), Teacher(1)),
            Event(1, 2, At(60), 3600, Teacher(1), Student(1)),
            Event(1, 4, At(61), 3600, Teacher(1), Student(3)));
        var second = (await world.Run.ExecuteAsync(SyncWorld.RunId(2), ct)).Meet!;

        Assert.Equal(3, second.EventsRead);
        Assert.Equal((0, 1), (second.SessionsAdded, second.SessionsUpdated));
        Assert.Equal((1, 2), (second.ParticipationsAdded, second.ParticipationsUpdated));
        Assert.Equal(0, second.NotOfTheSchool);
    }
}
