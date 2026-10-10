using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.RetentionPurgeTestData;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-032 AC-016, spec FR-016, db-design §6, PC-11: what the daily purge does with meeting-code links. A recent meeting
/// of a linked code keeps its course alive; an expiring course goes with its links, linked meetings and their
/// participations; a leaver's participations in the course's linked meetings go with the membership (email matched
/// without regard to case); an orphaned "not a course" mark goes, a course link with no meeting left stays; and the
/// run's audit row counts links, meetings and participations. One purge run through the use case the host registers,
/// over real PostgreSQL (TC-2); rows are seeded with raw SQL around the one cutoff <see cref="Cutoff"/>.
/// </summary>
public sealed class RetentionPurgeMeetLinkTests(PostgreSqlFixture database)
{
    private static string Code(int n) => MeetTestData.MeetingCode(n);

    private static string Conference(int n) => MeetTestData.ConferenceId(n);

    private static string Email(string local) => CourseTestData.Email(local);

    // ---------------------------------------------------------------- expiring course (FR-016 rule 2)

    [Fact]
    public async Task AnExpiredCourse_IsDeletedWithItsLinksAndLinkedMeetings()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var course = await MeetLinkRows.InsertDatedCourseAsync(host, 1, Old, Old, ct);
        await MeetLinkRows.InsertCourseLinkAsync(host, Code(1), course, Old, ct);
        await MeetLinkRows.InsertCourseLinkAsync(host, Code(2), course, Old, ct);
        var first = await MeetLinkRows.InsertMeetingAsync(host, Conference(1), Code(1), Old, ct, Email("a"), Email("b"));
        var second = await MeetLinkRows.InsertMeetingAsync(host, Conference(2), Code(2), Old, ct, Email("a"), null);

        var outcome = await host.RunPurgeAsync(ct);

        Assert.Empty(outcome.Failures);
        Assert.Equal(0L, await host.CountAsync("course", ct, "id = @id", ("id", course)));
        Assert.Equal(0L, await host.CountAsync("meeting_code_link", ct));
        Assert.Equal(0L, await host.CountAsync("meet_session", ct, "id IN (@a, @b)", ("a", first), ("b", second)));
        Assert.Equal(0L, await host.CountAsync("meet_participation", ct));
    }

    /// <summary>FR-016 rule 1: one linked meeting within N makes the course's last activity recent.</summary>
    [Fact]
    public async Task ARecentLinkedMeeting_KeepsTheCourseAndBothLinks()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var course = await MeetLinkRows.InsertDatedCourseAsync(host, 1, Old, Old, ct);
        await MeetLinkRows.InsertCourseLinkAsync(host, Code(1), course, Old, ct);
        await MeetLinkRows.InsertCourseLinkAsync(host, Code(2), course, Old, ct);
        await MeetLinkRows.InsertMeetingAsync(host, Conference(1), Code(1), Old, ct, Email("a"));
        var recent = await MeetLinkRows.InsertMeetingAsync(host, Conference(2), Code(2), Recent, ct, Email("a"), Email("b"));

        await host.RunPurgeAsync(ct);

        Assert.Equal(1L, await host.CountAsync("course", ct, "id = @id", ("id", course)));
        Assert.Equal(2L, await host.CountAsync("meeting_code_link", ct, "course_id = @id", ("id", course)));
        Assert.Equal(1L, await host.CountAsync("meet_session", ct, "id = @id", ("id", recent)));
        Assert.Equal(2L, await host.CountAsync("meet_participation", ct, "meet_session_id = @id", ("id", recent)));
    }

    /// <summary>FR-016 rule 1: meetings of an unassigned or a marked code count for no course.</summary>
    [Fact]
    public async Task RecentMeetingsOfAnUnassignedOrAMarkedCode_KeepNoCourseAlive()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var course = await MeetLinkRows.InsertDatedCourseAsync(host, 1, Old, Old, ct);
        await MeetLinkRows.InsertCourseLinkAsync(host, Code(1), course, Old, ct);
        await MeetLinkRows.InsertMeetingAsync(host, Conference(1), Code(1), Old, ct, Email("a"));
        await MeetLinkRows.InsertMarkAsync(host, Code(2), Old, ct);
        var marked = await MeetLinkRows.InsertMeetingAsync(host, Conference(2), Code(2), Recent, ct, Email("a"));
        var unassigned = await MeetLinkRows.InsertMeetingAsync(host, Conference(3), Code(3), Recent, ct, Email("a"));

        await host.RunPurgeAsync(ct);

        Assert.Equal(0L, await host.CountAsync("course", ct, "id = @id", ("id", course)));
        Assert.Equal(0L, await host.CountAsync("meeting_code_link", ct, "course_id IS NOT NULL"));
        Assert.Equal(1L, await host.CountAsync("meet_session", ct, "id = @id", ("id", marked)));
        Assert.Equal(1L, await host.CountAsync("meet_session", ct, "id = @id", ("id", unassigned)));
    }

    // ---------------------------------------------------------------- leaver (FR-016 rule 3)

    [Fact]
    public async Task ALeaversParticipations_InTheCoursesLinkedMeetings_AreDeleted_WhateverTheEmailCase()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var course = await MeetLinkRows.InsertDatedCourseAsync(host, 1, Recent, Old, ct);
        var member = await CourseRows.InsertParticipantAsync(host, ct, CourseTestData.UserId(1), Email("member"), CourseTestData.Name(1));
        var leaver = await CourseRows.InsertParticipantAsync(host, ct, CourseTestData.UserId(2), Email("leaver"), CourseTestData.Name(2));
        await CourseRows.InsertMembershipAsync(host, course, member, ct, onRoster: true, lastSeenAt: Now);
        await CourseRows.InsertMembershipAsync(
            host, course, leaver, ct, firstSeenAt: Old.AddDays(-100), lastSeenAt: Old, onRoster: false);
        await MeetLinkRows.InsertCourseLinkAsync(host, Code(1), course, Recent, ct);
        var linked = await MeetLinkRows.InsertMeetingAsync(
            host, Conference(1), Code(1), Recent, ct, Email("leaver").ToUpperInvariant(), Email("member"), null);
        var elsewhere = await MeetLinkRows.InsertMeetingAsync(host, Conference(2), Code(2), Recent, ct, Email("leaver"));

        await host.RunPurgeAsync(ct);

        Assert.Equal(0L, await host.CountAsync("course_membership", ct, "participant_id = @id", ("id", leaver)));
        Assert.Equal(
            0L,
            await host.CountAsync("meet_participation", ct, "meet_session_id = @id AND lower(email) = @email", ("id", linked), ("email", Email("leaver"))));
        Assert.Equal(2L, await host.CountAsync("meet_participation", ct, "meet_session_id = @id", ("id", linked)));
        Assert.Equal(1L, await host.CountAsync("meet_participation", ct, "meet_session_id = @id AND email = @email", ("id", linked), ("email", Email("member"))));
        Assert.Equal(1L, await host.CountAsync("meet_participation", ct, "meet_session_id = @id", ("id", elsewhere)));
    }

    // ---------------------------------------------------------------- marks (FR-016 rule 4)

    [Fact]
    public async Task AnOrphanedMarkGoes_AMarkWithARecentMeetingStays_ACourseLinkWithNoMeetingStays()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var course = await MeetLinkRows.InsertDatedCourseAsync(host, 1, Recent, Old, ct);
        await MeetLinkRows.InsertCourseLinkAsync(host, Code(1), course, Old, ct);
        await MeetLinkRows.InsertMarkAsync(host, Code(2), Old, ct);
        await MeetLinkRows.InsertMarkAsync(host, Code(3), Old, ct);
        await MeetLinkRows.InsertMarkAsync(host, Code(4), Old, ct);
        var oldMeeting = await MeetLinkRows.InsertMeetingAsync(host, Conference(2), Code(2), Old, ct, Email("a"));
        var recentMeeting = await MeetLinkRows.InsertMeetingAsync(host, Conference(3), Code(3), Recent, ct, Email("a"));

        await host.RunPurgeAsync(ct);

        Assert.Equal(0L, await host.CountAsync("meet_session", ct, "id = @id", ("id", oldMeeting)));
        Assert.Equal(0L, await host.CountAsync("meeting_code_link", ct, "meeting_code = @code", ("code", Code(2))));
        Assert.Equal(1L, await host.CountAsync("meet_session", ct, "id = @id", ("id", recentMeeting)));
        Assert.Equal(1L, await host.CountAsync("meeting_code_link", ct, "meeting_code = @code AND course_id IS NULL", ("code", Code(3))));
        Assert.Equal(0L, await host.CountAsync("meeting_code_link", ct, "meeting_code = @code", ("code", Code(4))));
        Assert.Equal(1L, await host.CountAsync("meeting_code_link", ct, "meeting_code = @code AND course_id = @id", ("code", Code(1)), ("id", course)));
    }

    // ---------------------------------------------------------------- audit counts (FR-016 rule 5)

    /// <summary>Links = course links with the expiring course + orphaned marks; meetings and connections include every rule.</summary>
    [Fact]
    public async Task TheAuditRow_CountsLinksMeetingsAndParticipations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var expired = await MeetLinkRows.InsertDatedCourseAsync(host, 1, Old, Old, ct);
        await MeetLinkRows.InsertCourseLinkAsync(host, Code(1), expired, Old, ct);
        await MeetLinkRows.InsertCourseLinkAsync(host, Code(2), expired, Old, ct);
        await MeetLinkRows.InsertMeetingAsync(host, Conference(1), Code(1), Old, ct, Email("a"), Email("b"));
        await MeetLinkRows.InsertMeetingAsync(host, Conference(2), Code(2), Old, ct, Email("a"), null);
        await MeetLinkRows.InsertMarkAsync(host, Code(3), Old, ct);
        await MeetLinkRows.InsertMeetingAsync(host, Conference(3), Code(3), Old, ct, Email("a"));
        var kept = await MeetLinkRows.InsertDatedCourseAsync(host, 2, Recent, Old, ct);
        var member = await CourseRows.InsertParticipantAsync(host, ct, CourseTestData.UserId(1), Email("member"), CourseTestData.Name(1));
        var leaver = await CourseRows.InsertParticipantAsync(host, ct, CourseTestData.UserId(2), Email("leaver"), CourseTestData.Name(2));
        await CourseRows.InsertMembershipAsync(host, kept, member, ct, onRoster: true, lastSeenAt: Now);
        await CourseRows.InsertMembershipAsync(host, kept, leaver, ct, firstSeenAt: Old.AddDays(-100), lastSeenAt: Old, onRoster: false);
        await MeetLinkRows.InsertCourseLinkAsync(host, Code(4), kept, Recent, ct);
        await MeetLinkRows.InsertMeetingAsync(host, Conference(4), Code(4), Recent, ct, Email("leaver").ToUpperInvariant(), Email("member"));

        await host.RunPurgeAsync(ct);

        Assert.Equal((3, 3, 6), await LinkCountsAsync(host, ct));
    }

    /// <summary>FR-016 rule 5: a run that removed no link records zero, not "absent".</summary>
    [Fact]
    public async Task APurgeWithNothingToDelete_RecordsZeroLinks()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var kept = await MeetLinkRows.InsertDatedCourseAsync(host, 1, Recent, Old, ct);
        await MeetLinkRows.InsertCourseLinkAsync(host, Code(1), kept, Recent, ct);
        await MeetLinkRows.InsertMeetingAsync(host, Conference(1), Code(1), Recent, ct, Email("a"));
        await MeetLinkRows.InsertMarkAsync(host, Code(2), Recent, ct);
        await MeetLinkRows.InsertMeetingAsync(host, Conference(2), Code(2), Recent, ct, Email("a"));

        await host.RunPurgeAsync(ct);

        Assert.Equal((0, 0, 0), await LinkCountsAsync(host, ct));
        Assert.Equal(2L, await host.CountAsync("meeting_code_link", ct));
    }

    /// <summary>The three counts of the one purge audit row, read directly (the shared reader has no such column).</summary>
    private static async Task<(int? Links, int? Sessions, int? Participations)> LinkCountsAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken)
    {
        var rows = await host.QueryAsync(
            """
            SELECT purged_meet_code_links, purged_meet_sessions, purged_meet_participations
            FROM audit_event WHERE action = 'retention_purge_run' ORDER BY id
            """,
            r => (
                Links: r.IsDBNull(0) ? (int?)null : r.GetInt32(0),
                Sessions: r.IsDBNull(1) ? (int?)null : r.GetInt32(1),
                Participations: r.IsDBNull(2) ? (int?)null : r.GetInt32(2)),
            cancellationToken);
        return Assert.Single(rows);
    }
}
