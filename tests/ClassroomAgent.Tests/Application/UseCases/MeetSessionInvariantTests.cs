using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.MeetTestData;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-031 entity model §1, §2 (spec FR-008, FR-009, I-2, I-4; db-design §2.2, §3.2): the meeting entity keeps start and
/// end over all its connections, holds one participation per endpoint, never changes its code or organizer, and
/// refuses the values its check constraints would refuse.
/// </summary>
public sealed class MeetSessionInvariantTests
{
    private static readonly DateTimeOffset T0 = InstallationTestHost.DefaultStart - TimeSpan.FromDays(2);

    private static DateTimeOffset At(int minutes) => T0 + TimeSpan.FromMinutes(minutes);

    private static MeetConnection Connection(int endpoint, int joinedAtMinute, int durationSeconds, string? email = null) =>
        new(EndpointId(endpoint), email, At(joinedAtMinute), durationSeconds);

    private static MeetSession Stored() =>
        MeetSession.Store(ConferenceId(1), MeetingCode(1), Teacher(1), Connection(1, 10, 1200, Teacher(1)));

    [Fact]
    public void ANewMeeting_TakesItsStartAndEndFromItsFirstConnection()
    {
        var session = Stored();

        Assert.Equal(ConferenceId(1), session.ConferenceId);
        Assert.Equal(MeetingCode(1), session.MeetingCode);
        Assert.Equal(Teacher(1), session.OrganizerEmail);
        Assert.Equal(At(10), session.StartedAt);
        Assert.Equal(At(30), session.EndedAt);
        var participation = Assert.Single(session.Participations);
        Assert.Equal((EndpointId(1), Teacher(1), At(10), 1200), (participation.EndpointId, participation.Email, participation.JoinedAt, participation.DurationSeconds));
    }

    /// <summary>I-2: start is the earliest join, end the latest leave — which need not be the latest join's.</summary>
    [Fact]
    public void RecordingConnections_RecomputesStartAndEnd()
    {
        var session = Stored();

        session.Record(Connection(2, 0, 300));
        session.Record(Connection(3, 20, 3600));

        Assert.Equal(At(0), session.StartedAt);
        Assert.Equal(At(80), session.EndedAt);
        Assert.Equal(3, session.Participations.Count);
    }

    /// <summary>FR-009: a known endpoint is updated in place, and start and end are recomputed — they may shrink too.</summary>
    [Fact]
    public void RecordingAKnownEndpoint_UpdatesIt_AndRecomputes()
    {
        var session = Stored();
        session.Record(Connection(2, 0, 7200));
        Assert.Equal((At(0), At(120)), (session.StartedAt, session.EndedAt));

        session.Record(Connection(2, 15, 60));

        Assert.Equal(2, session.Participations.Count);
        var second = session.Participations.Single(p => p.EndpointId == EndpointId(2));
        Assert.Equal((At(15), 60), (second.JoinedAt, second.DurationSeconds));
        Assert.Equal(At(10), session.StartedAt);
        Assert.Equal(At(30), session.EndedAt);
    }

    /// <summary>FR-008, AC-005: a connection with no domain email is an "other participant".</summary>
    [Fact]
    public void AConnectionWithoutADomainEmail_IsAnOtherParticipant()
    {
        var session = Stored();

        session.Record(Connection(2, 0, 60));

        var other = session.Participations.Single(p => p.EndpointId == EndpointId(2));
        Assert.Null(other.Email);
        Assert.True(other.IsOtherParticipant);
        Assert.False(session.Participations.Single(p => p.EndpointId == EndpointId(1)).IsOtherParticipant);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankConferenceId_IsRefused(string conferenceId) =>
        Assert.ThrowsAny<ArgumentException>(
            () => MeetSession.Store(conferenceId, MeetingCode(1), Teacher(1), Connection(1, 0, 60)));

    [Fact]
    public void AnOverlongConferenceId_IsRefused() =>
        Assert.ThrowsAny<ArgumentException>(
            () => MeetSession.Store(new string('c', MeetSession.ConferenceIdMaxLength + 1), MeetingCode(1), Teacher(1), Connection(1, 0, 60)));

    [Fact]
    public void ABlankOrOverlongMeetingCode_IsRefused()
    {
        Assert.ThrowsAny<ArgumentException>(
            () => MeetSession.Store(ConferenceId(1), " ", Teacher(1), Connection(1, 0, 60)));
        Assert.ThrowsAny<ArgumentException>(
            () => MeetSession.Store(ConferenceId(1), new string('m', MeetSession.MeetingCodeMaxLength + 1), Teacher(1), Connection(1, 0, 60)));
    }

    /// <summary>db-design §2.1: a meeting without an organizer email is never stored.</summary>
    [Fact]
    public void ABlankOrOverlongOrganizer_IsRefused()
    {
        Assert.ThrowsAny<ArgumentException>(
            () => MeetSession.Store(ConferenceId(1), MeetingCode(1), "", Connection(1, 0, 60)));
        Assert.ThrowsAny<ArgumentException>(
            () => MeetSession.Store(ConferenceId(1), MeetingCode(1), new string('o', MeetSession.EmailMaxLength + 1), Connection(1, 0, 60)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(MeetParticipation.MaxDurationSeconds + 1)]
    public void ADurationOutOfRange_IsRefused(int durationSeconds)
    {
        var session = Stored();

        Assert.ThrowsAny<ArgumentException>(() => session.Record(Connection(2, 0, durationSeconds)));
        Assert.Single(session.Participations);
    }

    [Fact]
    public void ABlankOrOverlongEndpoint_IsRefused()
    {
        var session = Stored();

        Assert.ThrowsAny<ArgumentException>(
            () => session.Record(new MeetConnection(" ", null, At(0), 60)));
        Assert.ThrowsAny<ArgumentException>(
            () => session.Record(new MeetConnection(new string('e', MeetParticipation.EndpointIdMaxLength + 1), null, At(0), 60)));
        Assert.Single(session.Participations);
    }

    [Fact]
    public void AnOverlongParticipantEmail_IsRefused()
    {
        var session = Stored();

        Assert.ThrowsAny<ArgumentException>(
            () => session.Record(Connection(2, 0, 60, new string('p', MeetSession.EmailMaxLength + 1))));
    }
}
