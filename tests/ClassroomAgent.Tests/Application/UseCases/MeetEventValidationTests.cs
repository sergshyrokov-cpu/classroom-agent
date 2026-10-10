using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.MeetTestData;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-031 AC-012 (spec VR-001, I-7, FR-004, FR-012): every <c>call_ended</c> event is external input, checked in
/// Application before anything is written. An event failing a rule is skipped and counted by reason, the rest of the
/// pull goes on; the boundary values each rule allows are stored. Each test pairs the invalid event of conference 1
/// with a valid event of conference 2, so "the pull continues" is asserted, not assumed.
/// </summary>
public sealed class MeetEventValidationTests
{
    public static TheoryData<string, MeetEventRejection> InvalidEvents => new()
    {
        { "conference-missing", MeetEventRejection.ConferenceId },
        { "conference-blank", MeetEventRejection.ConferenceId },
        { "conference-too-long", MeetEventRejection.ConferenceId },
        { "code-missing", MeetEventRejection.MeetingCode },
        { "code-blank", MeetEventRejection.MeetingCode },
        { "code-too-long", MeetEventRejection.MeetingCode },
        { "endpoint-missing", MeetEventRejection.EndpointId },
        { "endpoint-blank", MeetEventRejection.EndpointId },
        { "endpoint-too-long", MeetEventRejection.EndpointId },
        { "time-missing", MeetEventRejection.EventTime },
        { "time-before-the-window", MeetEventRejection.EventTime },
        { "time-after-the-window", MeetEventRejection.EventTime },
        { "duration-missing", MeetEventRejection.Duration },
        { "duration-negative", MeetEventRejection.Duration },
        { "duration-over-a-day", MeetEventRejection.Duration },
    };

    public static TheoryData<string> BoundaryEvents => new(
        "conference-128",
        "code-64",
        "endpoint-128",
        "duration-0",
        "duration-86400",
        "time-23h-before-the-window",
        "time-23h-after-the-window");

    /// <summary>A valid event of conference 1, a day before the run: the base every variant changes one value of.</summary>
    private static MeetCallEndedEvent Valid(DateTimeOffset now) =>
        Event(1, 1, now - TimeSpan.FromDays(1), 600, Teacher(1), Teacher(1));

    private static MeetCallEndedEvent Control(DateTimeOffset now) =>
        Event(2, 2, now - TimeSpan.FromDays(1), 600, Teacher(2), Teacher(2));

    /// <summary>The variant of <see cref="Valid"/> a case names. The first window is [now − horizon, now).</summary>
    private static MeetCallEndedEvent Variant(string name, DateTimeOffset now)
    {
        var valid = Valid(now);
        var from = now - Horizon;
        return name switch
        {
            "conference-missing" => valid with { ConferenceId = null },
            "conference-blank" => valid with { ConferenceId = "   " },
            "conference-too-long" => valid with { ConferenceId = new string('c', 129) },
            "conference-128" => valid with { ConferenceId = new string('c', 128) },
            "code-missing" => valid with { MeetingCode = null },
            "code-blank" => valid with { MeetingCode = " " },
            "code-too-long" => valid with { MeetingCode = new string('m', 65) },
            "code-64" => valid with { MeetingCode = new string('m', 64) },
            "endpoint-missing" => valid with { EndpointId = null },
            "endpoint-blank" => valid with { EndpointId = "  " },
            "endpoint-too-long" => valid with { EndpointId = new string('e', 129) },
            "endpoint-128" => valid with { EndpointId = new string('e', 128) },
            "time-missing" => valid with { OccurredAt = null },
            "time-before-the-window" => valid with { OccurredAt = from - TimeSpan.FromDays(1) - TimeSpan.FromMinutes(1) },
            "time-after-the-window" => valid with { OccurredAt = now + TimeSpan.FromDays(1) + TimeSpan.FromMinutes(1) },
            "time-23h-before-the-window" => valid with { OccurredAt = from - TimeSpan.FromHours(23) },
            "time-23h-after-the-window" => valid with { OccurredAt = now + TimeSpan.FromHours(23) },
            "duration-missing" => valid with { DurationSeconds = null },
            "duration-negative" => valid with { DurationSeconds = -1 },
            "duration-over-a-day" => valid with { DurationSeconds = 86_401 },
            "duration-0" => valid with { DurationSeconds = 0 },
            "duration-86400" => valid with { DurationSeconds = 86_400 },
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }

    /// <summary>
    /// AC-012, VR-001: an event failing a rule is skipped and counted under its reason; the valid event of the same page
    /// is stored and the run completes.
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidEvents))]
    public async Task AnInvalidEvent_IsSkippedAndCounted_AndThePullContinues(string variant, MeetEventRejection reason)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Meet.WithPage(Variant(variant, now), Control(now));

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.False(outcome.Failed);
        Assert.Equal(SyncRunStatus.Completed, world.States.Stored!.Status);
        Assert.Contains(ConferenceId(2), world.MeetSessions.Stored.Keys);
        Assert.Single(world.MeetSessions.Stored);
        Assert.Equal(1, outcome.Meet!.Skipped[reason]);
        Assert.Equal(1, outcome.Meet.SkippedTotal);
    }

    /// <summary>VR-001, I-7: the boundary values each rule allows are stored like any other event.</summary>
    [Theory]
    [MemberData(nameof(BoundaryEvents))]
    public async Task ABoundaryValue_IsAccepted(string variant)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Meet.WithPage(Variant(variant, now), Control(now));

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.Equal(2, world.MeetSessions.Stored.Count);
        Assert.Equal(0, outcome.Meet!.SkippedTotal);
    }

    /// <summary>VR-001: leading and trailing whitespace is trimmed; the values are otherwise stored as Google returned them.</summary>
    [Fact]
    public async Task Whitespace_IsTrimmed()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Meet.WithPage(Valid(now) with
        {
            ConferenceId = "  " + ConferenceId(1) + " ",
            MeetingCode = " " + MeetingCode(1) + "  ",
            OrganizerEmail = " " + Teacher(1) + " ",
            EndpointId = " " + EndpointId(1) + " ",
            Identifier = "  " + Student(1) + " ",
        });

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var session = Assert.Single(world.MeetSessions.Stored.Values);
        Assert.Equal(ConferenceId(1), session.ConferenceId);
        Assert.Equal(MeetingCode(1), session.MeetingCode);
        Assert.Equal(Teacher(1), session.OrganizerEmail);
        var participation = Assert.Single(session.Participations);
        Assert.Equal(EndpointId(1), participation.EndpointId);
        Assert.Equal(Student(1), participation.Email);
    }

    /// <summary>
    /// VR-001: an organizer email over 254 characters, even ending in the school's domain, is not a domain account — the
    /// meeting is counted as not of the school, not as an invalid event.
    /// </summary>
    [Fact]
    public async Task AnOverlongOrganizer_IsNotADomainAccount_NotAnInvalidEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        var overlong = new string('o', 255 - SchoolDomain.Length - 1) + "@" + SchoolDomain;
        Assert.Equal(255, overlong.Length);
        world.Meet.WithPage(Valid(now) with { OrganizerEmail = overlong }, Control(now));

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.DoesNotContain(ConferenceId(1), world.MeetSessions.Stored.Keys);
        Assert.Equal(1, outcome.Meet!.NotOfTheSchool);
        Assert.Equal(0, outcome.Meet.SkippedTotal);
    }

    /// <summary>VR-001: a participant identifier over 254 characters is an "other participant", and the connection is kept.</summary>
    [Fact]
    public async Task AnOverlongParticipant_IsAnOtherParticipant()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        var overlong = new string('p', 255 - SchoolDomain.Length - 1) + "@" + SchoolDomain;
        world.Meet.WithPage(Valid(now) with { Identifier = overlong });

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var participation = Assert.Single(world.MeetSessions.AllParticipations);
        Assert.Null(participation.Email);
    }

    /// <summary>
    /// VR-001, FR-005 (security review M-1): a malformed organizer email — an empty part before the <c>@</c> or more
    /// than one <c>@</c> — is not a domain account even when it ends in the school's domain; the meeting is counted as
    /// not of the school, not as an invalid event.
    /// </summary>
    [Theory]
    [InlineData("@" + SchoolDomain)]
    [InlineData("odd@local@" + SchoolDomain)]
    public async Task AMalformedOrganizer_IsNotADomainAccount_NotAnInvalidEvent(string malformed)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Meet.WithPage(Valid(now) with { OrganizerEmail = malformed }, Control(now));

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.DoesNotContain(ConferenceId(1), world.MeetSessions.Stored.Keys);
        Assert.Equal(1, outcome.Meet!.NotOfTheSchool);
        Assert.Equal(0, outcome.Meet.SkippedTotal);
    }

    /// <summary>
    /// VR-001, FR-008 (security review M-1): a malformed participant email ending in the school's domain is an "other
    /// participant" with no email, and the connection is kept.
    /// </summary>
    [Theory]
    [InlineData("@" + SchoolDomain)]
    [InlineData("odd@local@" + SchoolDomain)]
    public async Task AMalformedParticipant_IsAnOtherParticipant(string malformed)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Meet.WithPage(Valid(now) with { Identifier = malformed });

        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        var participation = Assert.Single(world.MeetSessions.AllParticipations);
        Assert.Null(participation.Email);
    }

    /// <summary>FR-004: events the adapter could not read at all are counted as skipped; the readable ones are stored.</summary>
    [Fact]
    public async Task UnreadableEvents_AreCountedAsSkipped_AndThePullContinues()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var now = world.Time.GetUtcNow();
        world.Meet.WithPage(2, Control(now));

        var outcome = await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);

        Assert.False(outcome.Failed);
        Assert.Contains(ConferenceId(2), world.MeetSessions.Stored.Keys);
        Assert.Equal(2, outcome.Meet!.Skipped[MeetEventRejection.Unreadable]);
    }
}
