using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-031 entity model §5, db-design §7, AC-001, AC-002, AC-003: the real repository and unit of work over real
/// PostgreSQL (TC-2) store a meeting with its connections, load it back with every value, recompute its start and end
/// when a connection is added, and never duplicate a connection that is recorded again.
/// </summary>
public sealed class MeetSessionRepositoryTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset Joined = new(2026, 9, 16, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A host that is not in read-only mode, so the commit backstop lets these direct writes through (BR-025), and that
    /// has no saved connection, so its own scheduled run is skipped.
    /// </summary>
    private Task<InstallationTestHost> StartWritableAsync(CancellationToken ct) =>
        SyncHostExtensions.StartAsync(database, ct, connection: SeededConnection.None);

    private static MeetSession NewSession(int conference = 1) =>
        MeetSession.Store(
            MeetTestData.ConferenceId(conference),
            MeetTestData.MeetingCode(conference),
            MeetTestData.Teacher(1),
            new MeetConnection(MeetTestData.EndpointId(1), MeetTestData.Student(1), Joined, 600));

    private static async Task SaveNewAsync(InstallationTestHost host, MeetSession session, CancellationToken ct)
    {
        using var scope = host.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMeetSessionRepository>().Add(session);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
    }

    private static async Task<IReadOnlyList<MeetSession>> LoadAsync(
        InstallationTestHost host,
        IReadOnlyCollection<string> conferenceIds,
        CancellationToken ct)
    {
        using var scope = host.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMeetSessionRepository>()
            .GetByConferenceIdsAsync(conferenceIds, ct);
    }

    /// <summary>AC-001: a stored meeting comes back with both connections and every value, null email included.</summary>
    [Fact]
    public async Task AStoredSession_RoundTripsWithItsParticipations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        var session = NewSession();
        session.Record(new MeetConnection(MeetTestData.EndpointId(2), null, Joined + TimeSpan.FromMinutes(5), 300));
        await SaveNewAsync(host, session, ct);

        var loaded = Assert.Single(await LoadAsync(host, [MeetTestData.ConferenceId(1)], ct));

        Assert.Equal(MeetTestData.ConferenceId(1), loaded.ConferenceId);
        Assert.Equal(MeetTestData.MeetingCode(1), loaded.MeetingCode);
        Assert.Equal(MeetTestData.Teacher(1), loaded.OrganizerEmail);
        Assert.Equal(Joined, loaded.StartedAt);
        Assert.Equal(Joined + TimeSpan.FromMinutes(10), loaded.EndedAt);
        Assert.NotEqual(default, loaded.CreatedAt);
        Assert.NotEqual(default, loaded.UpdatedAt);
        Assert.Equal(2, loaded.Participations.Count);
        var known = Assert.Single(loaded.Participations, p => p.EndpointId == MeetTestData.EndpointId(1));
        Assert.Equal(MeetTestData.Student(1), known.Email);
        Assert.Equal(Joined, known.JoinedAt);
        Assert.Equal(600, known.DurationSeconds);
        Assert.NotEqual(default, known.CreatedAt);
        Assert.NotEqual(default, known.UpdatedAt);
        var other = Assert.Single(loaded.Participations, p => p.EndpointId == MeetTestData.EndpointId(2));
        Assert.Null(other.Email);
        Assert.Equal(Joined + TimeSpan.FromMinutes(5), other.JoinedAt);
        Assert.Equal(300, other.DurationSeconds);
    }

    /// <summary>AC-002: a later connection is added to the stored meeting and its start and end are recomputed.</summary>
    [Fact]
    public async Task ALaterConnection_IsAddedAndTheBoundsAreRecomputed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        var session = NewSession();
        session.Record(new MeetConnection(MeetTestData.EndpointId(2), null, Joined + TimeSpan.FromMinutes(5), 300));
        await SaveNewAsync(host, session, ct);

        using (var scope = host.CreateScope())
        {
            var loaded = Assert.Single(await scope.ServiceProvider.GetRequiredService<IMeetSessionRepository>()
                .GetByConferenceIdsAsync([MeetTestData.ConferenceId(1)], ct));
            loaded.Record(new MeetConnection(MeetTestData.EndpointId(3), MeetTestData.Student(2), Joined + TimeSpan.FromHours(2), 1800));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }

        Assert.Equal(1L, await host.CountAsync("meet_session", ct));
        Assert.Equal(3L, await host.CountAsync("meet_participation", ct));
        Assert.Equal(Joined, new DateTimeOffset(await host.ScalarAsync<DateTime>("SELECT started_at FROM meet_session", ct), TimeSpan.Zero));
        Assert.Equal(
            Joined + TimeSpan.FromHours(2) + TimeSpan.FromMinutes(30),
            new DateTimeOffset(await host.ScalarAsync<DateTime>("SELECT ended_at FROM meet_session", ct), TimeSpan.Zero));
    }

    /// <summary>AC-003: recording a connection of an endpoint already stored leaves one row for it.</summary>
    [Fact]
    public async Task RecordingAStoredEndpointAgain_LeavesOneRowForIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await SaveNewAsync(host, NewSession(), ct);

        using (var scope = host.CreateScope())
        {
            var loaded = Assert.Single(await scope.ServiceProvider.GetRequiredService<IMeetSessionRepository>()
                .GetByConferenceIdsAsync([MeetTestData.ConferenceId(1)], ct));
            loaded.Record(new MeetConnection(MeetTestData.EndpointId(1), MeetTestData.Student(1), Joined, 600));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }

        Assert.Equal(1L, await host.CountAsync("meet_session", ct));
        Assert.Equal(1L, await host.CountAsync("meet_participation", ct, "endpoint_id = @id", ("id", MeetTestData.EndpointId(1))));
        Assert.Equal(1L, await host.CountAsync("meet_participation", ct));
    }

    [Fact]
    public async Task GetByConferenceIds_ReturnsOnlyTheStoredIdsOfAMixedList()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await SaveNewAsync(host, NewSession(1), ct);
        await SaveNewAsync(host, NewSession(2), ct);

        var loaded = await LoadAsync(host, [MeetTestData.ConferenceId(2), MeetTestData.ConferenceId(3), MeetTestData.ConferenceId(4)], ct);

        var only = Assert.Single(loaded);
        Assert.Equal(MeetTestData.ConferenceId(2), only.ConferenceId);
        Assert.Single(only.Participations);
    }

    [Fact]
    public async Task GetByConferenceIds_ReturnsNothingForUnknownIds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await SaveNewAsync(host, NewSession(1), ct);

        var loaded = await LoadAsync(host, [MeetTestData.ConferenceId(8), MeetTestData.ConferenceId(9)], ct);

        Assert.Empty(loaded);
    }
}
