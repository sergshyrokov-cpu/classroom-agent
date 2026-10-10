using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-032 entity model §5, db-design §2, §7 and §10, FR-001 and FR-013: the real <see cref="IMeetingCodeLinkRepository"/>
/// and unit of work over real PostgreSQL (TC-2) store a link in each of its states and load it back, answer the two
/// existence questions the write use cases ask, and turn a concurrent change of the same code — a stale concurrency
/// stamp, or a second first insert — into <see cref="MeetingCodeLinkConflictException"/>, never a provider exception.
/// </summary>
public sealed class MeetingCodeLinkRepositoryTests(PostgreSqlFixture database)
{
    private const long Person = MeetLinkRows.Person;

    private static readonly DateTimeOffset At = new(2026, 9, 16, 9, 0, 0, TimeSpan.Zero);

    private static readonly string Code = MeetTestData.MeetingCode(1);

    /// <summary>
    /// A host that is not in read-only mode, so the commit backstop lets these direct writes through (BR-025), and that
    /// has no saved connection, so its own scheduled run is skipped.
    /// </summary>
    private Task<InstallationTestHost> StartWritableAsync(CancellationToken ct) =>
        SyncHostExtensions.StartAsync(database, ct, connection: SeededConnection.None);

    private static async Task SaveNewAsync(InstallationTestHost host, MeetingCodeLink link, CancellationToken ct)
    {
        using var scope = host.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMeetingCodeLinkRepository>().Add(link);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
    }

    private static async Task<MeetingCodeLink?> LoadAsync(InstallationTestHost host, string code, CancellationToken ct)
    {
        using var scope = host.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMeetingCodeLinkRepository>().GetByCodeAsync(code, ct);
    }

    // ---------------------------------------------------------------- round trip (FR-001)

    [Fact]
    public async Task AnAutomaticLink_RoundTrips()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        await SaveNewAsync(host, MeetingCodeLink.LinkAutomatically(Code, course, At), ct);

        var loaded = await LoadAsync(host, Code, ct);

        Assert.NotNull(loaded);
        Assert.True(loaded.Id > 0);
        Assert.Equal(Code, loaded.MeetingCode);
        Assert.Equal(course, loaded.CourseId);
        Assert.True(loaded.LinkedAutomatically);
        Assert.Null(loaded.LinkedByAppUserId);
        Assert.Equal(At, loaded.LinkedAt);
        Assert.Null(loaded.ConfirmedByAppUserId);
        Assert.Null(loaded.ConfirmedAt);
        Assert.Null(loaded.MarkedByAppUserId);
        Assert.Null(loaded.MarkedAt);
        Assert.False(string.IsNullOrEmpty(loaded.ConcurrencyStamp));
        Assert.NotEqual(default, loaded.CreatedAt);
        Assert.NotEqual(default, loaded.UpdatedAt);
        Assert.Equal(MeetingCodeLinkState.Linked, loaded.State);
        Assert.True(loaded.CanBeConfirmed);
    }

    [Fact]
    public async Task APersonsLink_RoundTrips()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        await SaveNewAsync(host, MeetingCodeLink.LinkByPerson(Code, course, Person, At), ct);

        var loaded = await LoadAsync(host, Code, ct);

        Assert.NotNull(loaded);
        Assert.Equal(course, loaded.CourseId);
        Assert.False(loaded.LinkedAutomatically);
        Assert.Equal(Person, loaded.LinkedByAppUserId);
        Assert.Equal(At, loaded.LinkedAt);
        Assert.Null(loaded.ConfirmedAt);
        Assert.Equal(MeetingCodeLinkState.Linked, loaded.State);
        Assert.False(loaded.CanBeConfirmed);
    }

    [Fact]
    public async Task AMark_RoundTrips()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await SaveNewAsync(host, MeetingCodeLink.MarkNotACourse(Code, Person, At), ct);

        var loaded = await LoadAsync(host, Code, ct);

        Assert.NotNull(loaded);
        Assert.Null(loaded.CourseId);
        Assert.Null(loaded.LinkedAutomatically);
        Assert.Null(loaded.LinkedByAppUserId);
        Assert.Null(loaded.LinkedAt);
        Assert.Equal(Person, loaded.MarkedByAppUserId);
        Assert.Equal(At, loaded.MarkedAt);
        Assert.Equal(MeetingCodeLinkState.Marked, loaded.State);
    }

    /// <summary>A change of a stored row is saved and comes back with a new concurrency stamp (db-design §2.1).</summary>
    [Fact]
    public async Task AConfirmation_IsSaved_WithANewConcurrencyStamp()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        await SaveNewAsync(host, MeetingCodeLink.LinkAutomatically(Code, course, At), ct);
        var before = await LoadAsync(host, Code, ct);
        Assert.NotNull(before);
        var confirmedAt = At + TimeSpan.FromHours(2);

        using (var scope = host.CreateScope())
        {
            var link = await scope.ServiceProvider.GetRequiredService<IMeetingCodeLinkRepository>().GetByCodeAsync(Code, ct);
            Assert.NotNull(link);
            link.Confirm(Person, confirmedAt);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }

        var after = await LoadAsync(host, Code, ct);
        Assert.NotNull(after);
        Assert.Equal(Person, after.ConfirmedByAppUserId);
        Assert.Equal(confirmedAt, after.ConfirmedAt);
        Assert.True(after.LinkedAutomatically);
        Assert.False(after.CanBeConfirmed);
        Assert.NotEqual(before.ConcurrencyStamp, after.ConcurrencyStamp);
    }

    [Fact]
    public async Task AnUnknownCode_IsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        await MeetLinkRows.InsertCourseLinkAsync(host, Code, course, At, ct);

        Assert.Null(await LoadAsync(host, MeetTestData.MeetingCode(2), ct));
    }

    // ---------------------------------------------------------------- existence questions (FR-001, VR-001, VR-002)

    [Fact]
    public async Task CodeHasMeetings_IsTrueOnlyForAnExactCodeOfAStoredMeeting()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await MeetLinkRows.InsertSessionAsync(host, MeetTestData.ConferenceId(1), Code, At, ct);
        using var scope = host.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMeetingCodeLinkRepository>();

        Assert.True(await repository.CodeHasMeetingsAsync(Code, ct));
        Assert.False(await repository.CodeHasMeetingsAsync(Code.ToUpperInvariant(), ct));
        Assert.False(await repository.CodeHasMeetingsAsync(MeetTestData.MeetingCode(2), ct));
    }

    /// <summary>A link row without a meeting is not "a code with meetings": the question is about <c>meet_session</c> only.</summary>
    [Fact]
    public async Task CodeHasMeetings_IsFalseForACodeThatOnlyHasALink()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await MeetLinkRows.InsertMarkAsync(host, Code, At, ct);
        using var scope = host.CreateScope();

        Assert.False(await scope.ServiceProvider.GetRequiredService<IMeetingCodeLinkRepository>().CodeHasMeetingsAsync(Code, ct));
    }

    [Fact]
    public async Task CourseExists_IsTrueForAStoredCourseAndFalseOtherwise()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        using var scope = host.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMeetingCodeLinkRepository>();

        Assert.True(await repository.CourseExistsAsync(course, ct));
        Assert.False(await repository.CourseExistsAsync(course + 1_000, ct));
    }

    // ---------------------------------------------------------------- concurrency (FR-013, db-design §7)

    /// <summary>Two people load the same link and both change it: the second save fails on the concurrency stamp.</summary>
    [Fact]
    public async Task TwoChangesOfOneLink_TheSecondSaveIsAConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        var first = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(1));
        var second = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(2));
        await SaveNewAsync(host, MeetingCodeLink.LinkAutomatically(Code, first, At), ct);
        using var scopeA = host.CreateScope();
        using var scopeB = host.CreateScope();
        var linkA = await scopeA.ServiceProvider.GetRequiredService<IMeetingCodeLinkRepository>().GetByCodeAsync(Code, ct);
        var linkB = await scopeB.ServiceProvider.GetRequiredService<IMeetingCodeLinkRepository>().GetByCodeAsync(Code, ct);
        Assert.NotNull(linkA);
        Assert.NotNull(linkB);
        linkA.Relink(second, Person, At + TimeSpan.FromHours(1));
        linkB.Mark(Person + 1, At + TimeSpan.FromHours(2));

        await scopeA.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        await Assert.ThrowsAsync<MeetingCodeLinkConflictException>(
            () => scopeB.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct));

        var stored = await LoadAsync(host, Code, ct);
        Assert.NotNull(stored);
        Assert.Equal(second, stored.CourseId);
        Assert.Equal(Person, stored.LinkedByAppUserId);
    }

    /// <summary>Two people decide the same unassigned code at once: the unique index lets one insert through.</summary>
    [Fact]
    public async Task TwoFirstDecisionsOnOneCode_TheSecondSaveIsAConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        var first = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(1));
        var second = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(2));
        using var scopeA = host.CreateScope();
        using var scopeB = host.CreateScope();
        scopeA.ServiceProvider.GetRequiredService<IMeetingCodeLinkRepository>()
            .Add(MeetingCodeLink.LinkByPerson(Code, first, Person, At));
        scopeB.ServiceProvider.GetRequiredService<IMeetingCodeLinkRepository>()
            .Add(MeetingCodeLink.LinkAutomatically(Code, second, At));

        await scopeA.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        await Assert.ThrowsAsync<MeetingCodeLinkConflictException>(
            () => scopeB.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct));

        Assert.Equal(1L, await host.CountAsync("meeting_code_link", ct));
        var stored = await LoadAsync(host, Code, ct);
        Assert.NotNull(stored);
        Assert.Equal(first, stored.CourseId);
    }
}
