using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.RetentionPurgeTestData;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-037 AC-001 … AC-005, AC-007, AC-008, AC-010, AC-014, AC-015 and AC-017: one purge run through the use case the
/// host registers, over the host's real PostgreSQL database (TC-2). Rows are seeded with raw SQL around the one cutoff
/// <see cref="Cutoff"/> (<c>Retention:Years</c> = 5 at <see cref="Now"/>), so each test states exactly which dates
/// expire. The host runs without its purge background service, so the run under test is the only one.
/// </summary>
public sealed class RetentionPurgeTests(PostgreSqlFixture database)
{
    // ---------------------------------------------------------------- AC-001, AC-002, AC-014, AC-015: courses

    /// <summary>AC-001: everything in an expired course goes — submissions with grades, coursework, memberships.</summary>
    [Theory]
    [InlineData(CourseTestData.States.Active)]
    [InlineData(CourseTestData.States.Archived)]
    public async Task AnExpiredCourse_IsDeletedWithEverythingInIt_WhateverItsState(string state)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var course = await SeedCourseAsync(host, 1, ct, state: state, courseUpdate: Old, itemUpdate: Old, submissionUpdate: Old);

        await host.RunPurgeAsync(ct);

        Assert.Equal(0L, await host.CountAsync("course", ct, "id = @id", ("id", course.CourseId)));
        Assert.Equal(0L, await host.CountAsync("course_work", ct, "course_id = @id", ("id", course.CourseId)));
        Assert.Equal(0L, await host.CountAsync("course_membership", ct, "course_id = @id", ("id", course.CourseId)));
        Assert.Equal(0L, await host.CountAsync("submission", ct, "id = @id", ("id", course.SubmissionId)));
    }

    /// <summary>AC-002: one coursework item updated within N keeps the whole course.</summary>
    [Fact]
    public async Task ARecentCourseworkUpdate_KeepsTheWholeCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var course = await SeedCourseAsync(host, 1, ct, courseUpdate: Old, itemUpdate: Recent, submissionUpdate: Old);

        await host.RunPurgeAsync(ct);

        await AssertCourseWholeAsync(host, course, ct);
    }

    /// <summary>AC-002: the creation of an item counts as activity too (FR-002).</summary>
    [Fact]
    public async Task ARecentCourseworkCreation_KeepsTheWholeCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var course = await SeedCourseAsync(host, 1, ct, courseUpdate: Old, itemCreation: Recent, itemUpdate: null, submissionUpdate: Old);

        await host.RunPurgeAsync(ct);

        await AssertCourseWholeAsync(host, course, ct);
    }

    /// <summary>AC-002: one submission changed within N keeps the whole course.</summary>
    [Fact]
    public async Task ARecentSubmissionChange_KeepsTheWholeCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var course = await SeedCourseAsync(host, 1, ct, courseUpdate: Old, itemUpdate: Old, submissionUpdate: Recent);

        await host.RunPurgeAsync(ct);

        await AssertCourseWholeAsync(host, course, ct);
    }

    [Fact]
    public async Task ARecentCourseUpdate_KeepsTheCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var course = await SeedCourseAsync(host, 1, ct, courseUpdate: Recent, itemUpdate: Old, submissionUpdate: Old);

        await host.RunPurgeAsync(ct);

        await AssertCourseWholeAsync(host, course, ct);
    }

    /// <summary>AC-014: last activity exactly on the cutoff is kept; one tick earlier is deleted.</summary>
    [Fact]
    public async Task TheCourseBoundary_IsStrict()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var onCutoff = await SeedCourseAsync(host, 1, ct, courseUpdate: Cutoff, itemUpdate: Old, submissionUpdate: Old);
        var beforeCutoff = await SeedCourseAsync(host, 2, ct, courseUpdate: Cutoff.AddTicks(-10), itemUpdate: Old, submissionUpdate: Old);

        await host.RunPurgeAsync(ct);

        await AssertCourseWholeAsync(host, onCutoff, ct);
        Assert.Equal(0L, await host.CountAsync("course", ct, "id = @id", ("id", beforeCutoff.CourseId)));
    }

    /// <summary>AC-015, OD-006: with no Google date at all, the course's own <c>created_at</c> decides.</summary>
    [Fact]
    public async Task ACourseWithNoGoogleDate_IsJudgedByWhenItWasImported()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var importedLongAgo = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(1));
        var importedRecently = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(2));
        await host.ExecuteAsync(
            "UPDATE course SET created_at = @old, updated_at = @old WHERE id = @id",
            ct,
            ("old", Old),
            ("id", importedLongAgo));
        await host.ExecuteAsync(
            "UPDATE course SET created_at = @recent, updated_at = @recent WHERE id = @id",
            ct,
            ("recent", Recent),
            ("id", importedRecently));

        await host.RunPurgeAsync(ct);

        Assert.Equal(0L, await host.CountAsync("course", ct, "id = @id", ("id", importedLongAgo)));
        Assert.Equal(1L, await host.CountAsync("course", ct, "id = @id", ("id", importedRecently)));
    }

    /// <summary>AC-015: a Google date, even an old one, takes precedence over a recent import time.</summary>
    [Fact]
    public async Task AnOldGoogleDate_OutranksARecentImport()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var course = await SeedCourseAsync(host, 1, ct, courseUpdate: Old, itemUpdate: Old, submissionUpdate: Old);
        await host.ExecuteAsync("UPDATE course SET created_at = @now, updated_at = @now", ct, ("now", Now));

        await host.RunPurgeAsync(ct);

        Assert.Equal(0L, await host.CountAsync("course", ct, "id = @id", ("id", course.CourseId)));
    }

    // ---------------------------------------------------------------- AC-003, AC-004: leavers, participants

    /// <summary>
    /// AC-003: in a kept course, an off-roster member last seen before the cutoff goes with their submissions there;
    /// an off-roster member seen within N, the roster and the course stay.
    /// </summary>
    [Fact]
    public async Task AnExpiredLeaver_IsDeletedWithTheirSubmissions_AndOnlyThey()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var course = await SeedCourseAsync(host, 1, ct, courseUpdate: Recent, itemUpdate: Recent, submissionUpdate: Recent);
        var expiredLeaver = await AddMemberAsync(host, course, 11, ct, onRoster: false, lastSeen: Old, withSubmission: true);
        var recentLeaver = await AddMemberAsync(host, course, 12, ct, onRoster: false, lastSeen: Recent, withSubmission: true);
        var oldRosterMember = await AddMemberAsync(host, course, 13, ct, onRoster: true, lastSeen: Old, withSubmission: true);

        await host.RunPurgeAsync(ct);

        Assert.Equal(0L, await MembershipCountAsync(host, course.CourseId, expiredLeaver.ParticipantId, ct));
        Assert.Equal(0L, await host.CountAsync("submission", ct, "id = @id", ("id", expiredLeaver.SubmissionId!.Value)));
        Assert.Equal(1L, await MembershipCountAsync(host, course.CourseId, recentLeaver.ParticipantId, ct));
        Assert.Equal(1L, await host.CountAsync("submission", ct, "id = @id", ("id", recentLeaver.SubmissionId!.Value)));
        Assert.Equal(1L, await MembershipCountAsync(host, course.CourseId, oldRosterMember.ParticipantId, ct));
        Assert.Equal(1L, await host.CountAsync("submission", ct, "id = @id", ("id", oldRosterMember.SubmissionId!.Value)));
        await AssertCourseWholeAsync(host, course, ct);
    }

    /// <summary>AC-003, FR-005: a leaver's submissions in <b>another</b> course are not this course's to delete.</summary>
    [Fact]
    public async Task ALeaversSubmissionsInAnotherCourse_Stay()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var left = await SeedCourseAsync(host, 1, ct, courseUpdate: Recent, itemUpdate: Recent, submissionUpdate: Recent);
        var stayed = await SeedCourseAsync(host, 2, ct, courseUpdate: Recent, itemUpdate: Recent, submissionUpdate: Recent);
        var person = await AddMemberAsync(host, left, 11, ct, onRoster: false, lastSeen: Old, withSubmission: true);
        await CourseRows.InsertMembershipAsync(host, stayed.CourseId, person.ParticipantId, ct, onRoster: true);
        var elsewhere = await CourseWorkRows.InsertSubmissionAsync(
            host,
            stayed.CourseWorkId,
            person.ParticipantId,
            ct,
            googleId: CourseWorkTestData.SubmissionId(99),
            updateTime: Recent);

        await host.RunPurgeAsync(ct);

        Assert.Equal(0L, await MembershipCountAsync(host, left.CourseId, person.ParticipantId, ct));
        Assert.Equal(1L, await host.CountAsync("submission", ct, "id = @id", ("id", elsewhere)));
        Assert.Equal(1L, await host.CountAsync("classroom_participant", ct, "id = @id", ("id", person.ParticipantId)));
    }

    /// <summary>AC-014 for leavers: "last seen" on the cutoff is kept, one tick earlier is not.</summary>
    [Fact]
    public async Task TheLeaverBoundary_IsStrict()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var course = await SeedCourseAsync(host, 1, ct, courseUpdate: Recent, itemUpdate: Recent, submissionUpdate: Recent);
        var onCutoff = await AddMemberAsync(host, course, 11, ct, onRoster: false, lastSeen: Cutoff, withSubmission: false);
        var beforeCutoff = await AddMemberAsync(host, course, 12, ct, onRoster: false, lastSeen: Cutoff.AddTicks(-10), withSubmission: false);

        await host.RunPurgeAsync(ct);

        Assert.Equal(1L, await MembershipCountAsync(host, course.CourseId, onCutoff.ParticipantId, ct));
        Assert.Equal(0L, await MembershipCountAsync(host, course.CourseId, beforeCutoff.ParticipantId, ct));
    }

    /// <summary>AC-004: a participant left with no membership goes; one with a membership elsewhere stays.</summary>
    [Fact]
    public async Task AnOrphanedParticipant_IsDeleted_AndOneStillEnrolledElsewhereIsKept()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var expired = await SeedCourseAsync(host, 1, ct, courseUpdate: Old, itemUpdate: Old, submissionUpdate: Old);
        var kept = await SeedCourseAsync(host, 2, ct, courseUpdate: Recent, itemUpdate: Recent, submissionUpdate: Recent);
        var onlyThere = await AddMemberAsync(host, expired, 11, ct, onRoster: true, lastSeen: Old, withSubmission: false);
        var alsoElsewhere = await AddMemberAsync(host, expired, 12, ct, onRoster: true, lastSeen: Old, withSubmission: false);
        await CourseRows.InsertMembershipAsync(host, kept.CourseId, alsoElsewhere.ParticipantId, ct, onRoster: true);

        await host.RunPurgeAsync(ct);

        Assert.Equal(0L, await host.CountAsync("classroom_participant", ct, "id = @id", ("id", onlyThere.ParticipantId)));
        Assert.Equal(1L, await host.CountAsync("classroom_participant", ct, "id = @id", ("id", alsoElsewhere.ParticipantId)));
    }

    /// <summary>AC-004: a leaver deleted by FR-005 who has no other course is an orphan in the same run.</summary>
    [Fact]
    public async Task ALeaverWithNoOtherCourse_IsDeletedAsAParticipantInTheSameRun()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var course = await SeedCourseAsync(host, 1, ct, courseUpdate: Recent, itemUpdate: Recent, submissionUpdate: Recent);
        var leaver = await AddMemberAsync(host, course, 11, ct, onRoster: false, lastSeen: Old, withSubmission: true);

        await host.RunPurgeAsync(ct);

        Assert.Equal(0L, await host.CountAsync("classroom_participant", ct, "id = @id", ("id", leaver.ParticipantId)));
    }

    // ---------------------------------------------------------------- AC-005, AC-014: accounts

    /// <summary>
    /// AC-005: expired Admin and Dean accounts go — one of them disabled, and a Dean who never signed in, judged by
    /// creation — while accounts used within N stay; audit rows naming the deleted ones keep their ids.
    /// </summary>
    [Fact]
    public async Task UnusedAccounts_AreDeleted_AndAuditRowsKeepTheirIds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var oldAdmin = await host.InsertAccountAsync("old-admin@school.test", "admin", Old.AddYears(-1), Old, ct);
        var oldDisabledDean = await host.InsertAccountAsync("old-dean@school.test", "dean", Old.AddYears(-1), Old, ct, isDisabled: true);
        var neverSignedIn = await host.InsertAccountAsync("never@school.test", "dean", Old, null, ct);
        var activeAdmin = await host.InsertAccountAsync("admin@school.test", "admin", Old, Recent, ct);
        var newDean = await host.InsertAccountAsync("new-dean@school.test", "dean", Recent, null, ct);
        var namingRow = await host.InsertAuditRowAsync(Recent, ct, actorId: oldAdmin, targetType: "app_user", targetId: oldDisabledDean);

        await host.RunPurgeAsync(ct);

        var remaining = (await host.AppUsersAsync(ct)).Select(u => u.Id).ToHashSet();
        Assert.DoesNotContain(oldAdmin, remaining);
        Assert.DoesNotContain(oldDisabledDean, remaining);
        Assert.DoesNotContain(neverSignedIn, remaining);
        Assert.Contains(activeAdmin, remaining);
        Assert.Contains(newDean, remaining);
        var row = Assert.Single(await host.AuditRowsAsync(ct), r => r.Id == namingRow);
        Assert.Equal(oldAdmin, row.ActorId);
        Assert.Equal(oldDisabledDean, row.TargetId);
    }

    /// <summary>AC-014 for accounts: a last sign-in on the cutoff is kept, one tick earlier is not.</summary>
    [Fact]
    public async Task TheAccountBoundary_IsStrict()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var onCutoff = await host.InsertAccountAsync("on@school.test", "dean", Old, Cutoff, ct);
        var beforeCutoff = await host.InsertAccountAsync("before@school.test", "dean", Old, Cutoff.AddTicks(-10), ct);

        await host.RunPurgeAsync(ct);

        var remaining = (await host.AppUsersAsync(ct)).Select(u => u.Id).ToList();
        Assert.Contains(onCutoff, remaining);
        Assert.DoesNotContain(beforeCutoff, remaining);
    }

    // ---------------------------------------------------------------- AC-007, AC-014: audit rows

    /// <summary>
    /// AC-007: exactly the rows older than the cutoff go — including a row naming a course deleted in the same run —
    /// and nothing newer (PC-11). The boundary row stays (AC-014).
    /// </summary>
    [Fact]
    public async Task ExactlyTheAuditRowsOlderThanTheCutoff_AreDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var course = await SeedCourseAsync(host, 1, ct, courseUpdate: Old, itemUpdate: Old, submissionUpdate: Old);
        var old = await host.InsertAuditRowAsync(Old, ct);
        var justBefore = await host.InsertAuditRowAsync(Cutoff.AddTicks(-10), ct);
        var onCutoff = await host.InsertAuditRowAsync(Cutoff, ct);
        var recentNamingTheCourse = await host.InsertAuditRowAsync(Recent, ct, targetId: course.CourseId);
        var existingBefore = (await host.AuditRowsAsync(ct)).Select(r => r.Id).ToList();

        await host.RunPurgeAsync(ct);

        var remaining = (await host.AuditRowsAsync(ct)).Select(r => r.Id).ToList();
        Assert.DoesNotContain(old, remaining);
        Assert.DoesNotContain(justBefore, remaining);
        Assert.Contains(onCutoff, remaining);
        Assert.Contains(recentNamingTheCourse, remaining);
        Assert.Equal(existingBefore.Except([old, justBefore]).Order(), remaining.Order());
    }

    // ---------------------------------------------------------------- AC-008: the run's audit event

    /// <summary>AC-008: one row with actor <c>system</c> and the five counts of what this run removed.</summary>
    [Fact]
    public async Task TheRun_WritesOneAuditEventWithTheCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var expired = await SeedCourseAsync(host, 1, ct, courseUpdate: Old, itemUpdate: Old, submissionUpdate: Old);
        var kept = await SeedCourseAsync(host, 2, ct, courseUpdate: Recent, itemUpdate: Recent, submissionUpdate: Recent);
        await AddMemberAsync(host, kept, 11, ct, onRoster: false, lastSeen: Old, withSubmission: false);
        await AddMemberAsync(host, kept, 12, ct, onRoster: false, lastSeen: Old, withSubmission: false);
        await host.InsertAccountAsync("old@school.test", "dean", Old, Old, ct);
        await host.InsertAuditRowAsync(Old, ct);
        await host.InsertAuditRowAsync(Old, ct);
        await host.InsertAuditRowAsync(Old, ct);

        var outcome = await host.RunPurgeAsync(ct);

        // Participants: the expired course's one member, plus the two leavers who had no other course.
        var row = Assert.Single(await host.PurgeAuditRowsAsync(ct));
        Assert.Equal("system", row.ActorType);
        Assert.Null(row.ActorId);
        Assert.Null(row.ActorRole);
        Assert.Null(row.TargetType);
        Assert.Null(row.TargetId);
        Assert.Equal("succeeded", row.Outcome);
        Assert.Null(row.RequestId);
        Assert.Equal(Now, row.OccurredAt);
        Assert.Equal((1, 2, 3, 1, 3), (row.Courses, row.LeaverMemberships, row.Participants, row.Accounts, row.AuditRows));
        Assert.Equal(new RetentionPurgeOutcome(Cutoff, new(1, 2, 3, 1, 3, 0, 0), []).Counts, outcome.Counts);
        Assert.Equal(Cutoff, outcome.Cutoff);
        Assert.Empty(outcome.Failures);
        Assert.Equal(1L, await host.CountAsync("course", ct, "id = @id", ("id", kept.CourseId)));
        Assert.Equal(0L, await host.CountAsync("course", ct, "id = @id", ("id", expired.CourseId)));
    }

    /// <summary>AC-008: a run that removed nothing still leaves exactly one row, all counts zero.</summary>
    [Fact]
    public async Task ARunThatRemovesNothing_StillWritesOneAuditEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        await SeedCourseAsync(host, 1, ct, courseUpdate: Recent, itemUpdate: Recent, submissionUpdate: Recent);

        await host.RunPurgeAsync(ct);

        var row = Assert.Single(await host.PurgeAuditRowsAsync(ct));
        Assert.Equal((0, 0, 0, 0, 0), (row.Courses, row.LeaverMemberships, row.Participants, row.Accounts, row.AuditRows));
    }

    /// <summary>AC-008: the run's own row is written after the audit-row delete, so a second run does not count it.</summary>
    [Fact]
    public async Task EachRun_WritesItsOwnRow_AndTheRowsSurviveTheNextRun()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);

        await host.RunPurgeAsync(ct);
        await host.RunPurgeAsync(ct);

        var rows = await host.PurgeAuditRowsAsync(ct);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(0, r.AuditRows));
    }

    /// <summary>AC-008, SC-10: no name, email, grade or Google id of what was purged appears in the audit trail.</summary>
    [Fact]
    public async Task TheAuditEvent_CarriesNoPersonalData()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var course = await SeedCourseAsync(host, 1, ct, courseUpdate: Old, itemUpdate: Old, submissionUpdate: Old);
        await host.ExecuteAsync("UPDATE submission SET assigned_grade = 87.5 WHERE id = @id", ct, ("id", course.SubmissionId));
        await host.InsertAccountAsync("purged.dean@school.test", "dean", Old, Old, ct);

        await host.RunPurgeAsync(ct);

        var json = string.Join('\n', await host.AuditRowsAsJsonAsync(ct));
        Assert.Contains(RetentionPurgeTestData.Action, json, StringComparison.Ordinal);
        Assert.DoesNotContain(CourseTestData.CourseId(1), json, StringComparison.Ordinal);
        Assert.DoesNotContain(CourseTestData.CourseName(1), json, StringComparison.Ordinal);
        Assert.DoesNotContain(CourseTestData.UserId(1), json, StringComparison.Ordinal);
        Assert.DoesNotContain(CourseTestData.Email("member1"), json, StringComparison.Ordinal);
        Assert.DoesNotContain(CourseTestData.Name(1), json, StringComparison.Ordinal);
        Assert.DoesNotContain("purged.dean", json, StringComparison.Ordinal);
        Assert.DoesNotContain("87.5", json, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- AC-010, AC-017: failures

    /// <summary>
    /// AC-010: a failure inside one course's transaction leaves it whole; the other expired courses go; the run's
    /// counts leave it out; the outcome names it by internal id; the next run retries and deletes it.
    /// </summary>
    [Fact]
    public async Task AFailingCourse_IsLeftWhole_AndTheOthersAreDeleted_AndTheNextRunRetries()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var failing = await SeedCourseAsync(host, 1, ct, courseUpdate: Old, itemUpdate: Old, submissionUpdate: Old);
        var other = await SeedCourseAsync(host, 2, ct, courseUpdate: Old, itemUpdate: Old, submissionUpdate: Old);
        await host.FailDeletesAsync("course", ct, failing.CourseId);

        var outcome = await host.RunPurgeAsync(ct);

        await AssertCourseWholeAsync(host, failing, ct);
        Assert.Equal(0L, await host.CountAsync("course", ct, "id = @id", ("id", other.CourseId)));
        var failure = Assert.Single(outcome.Failures);
        Assert.Equal(RetentionPurgeStep.ExpiredCourse, failure.Step);
        Assert.Equal(failing.CourseId, failure.CourseId);
        Assert.False(string.IsNullOrWhiteSpace(failure.ExceptionType));
        Assert.DoesNotContain("injected", failure.ExceptionType, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, Assert.Single(await host.PurgeAuditRowsAsync(ct)).Courses);

        await host.StopFailingDeletesAsync("course", ct);
        await host.RunPurgeAsync(ct);

        Assert.Equal(0L, await host.CountAsync("course", ct, "id = @id", ("id", failing.CourseId)));
        Assert.Equal(1, (await host.PurgeAuditRowsAsync(ct))[1].Courses);
    }

    /// <summary>
    /// AC-017, FR-009: a failing step (here: accounts) rolls back and is reported; the later step (audit rows) and
    /// the run's audit event still happen.
    /// </summary>
    [Fact]
    public async Task AFailingStep_IsRolledBack_AndTheLaterStepsStillRun()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var first = await host.InsertAccountAsync("first@school.test", "dean", Old, Old, ct);
        var second = await host.InsertAccountAsync("second@school.test", "dean", Old, Old, ct);
        await host.FailDeletesAsync("app_user", ct, second);
        var oldRow = await host.InsertAuditRowAsync(Old, ct);

        var outcome = await host.RunPurgeAsync(ct);

        var remaining = (await host.AppUsersAsync(ct)).Select(u => u.Id).ToList();
        Assert.Contains(first, remaining);
        Assert.Contains(second, remaining);
        Assert.Equal(RetentionPurgeStep.Accounts, Assert.Single(outcome.Failures).Step);
        Assert.Null(outcome.Failures[0].CourseId);
        Assert.DoesNotContain(oldRow, (await host.AuditRowsAsync(ct)).Select(r => r.Id));
        var row = Assert.Single(await host.PurgeAuditRowsAsync(ct));
        Assert.Equal((0, 1), (row.Accounts, row.AuditRows));
    }

    /// <summary>AC-010 for leavers: a failing leaver delete leaves that course's leavers whole and the rest goes on.</summary>
    [Fact]
    public async Task AFailingLeaverDelete_IsRolledBackForThatCourseOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var failingCourse = await SeedCourseAsync(host, 1, ct, courseUpdate: Recent, itemUpdate: Recent, submissionUpdate: Recent);
        var otherCourse = await SeedCourseAsync(host, 2, ct, courseUpdate: Recent, itemUpdate: Recent, submissionUpdate: Recent);
        var stuck = await AddMemberAsync(host, failingCourse, 11, ct, onRoster: false, lastSeen: Old, withSubmission: true);
        var gone = await AddMemberAsync(host, otherCourse, 12, ct, onRoster: false, lastSeen: Old, withSubmission: true);
        await host.FailDeletesAsync("course_membership", ct, await MembershipIdAsync(host, failingCourse.CourseId, stuck.ParticipantId, ct));

        var outcome = await host.RunPurgeAsync(ct);

        Assert.Equal(1L, await MembershipCountAsync(host, failingCourse.CourseId, stuck.ParticipantId, ct));
        Assert.Equal(1L, await host.CountAsync("submission", ct, "id = @id", ("id", stuck.SubmissionId!.Value)));
        Assert.Equal(0L, await MembershipCountAsync(host, otherCourse.CourseId, gone.ParticipantId, ct));
        var failure = Assert.Single(outcome.Failures);
        Assert.Equal((RetentionPurgeStep.Leavers, (long?)failingCourse.CourseId), (failure.Step, failure.CourseId));
        Assert.Equal(1, Assert.Single(await host.PurgeAuditRowsAsync(ct)).LeaverMemberships);
    }

    // ---------------------------------------------------------------- helpers

    private static async Task<SeededCourse> SeedCourseAsync(
        InstallationTestHost host,
        int ordinal,
        CancellationToken cancellationToken,
        string state = CourseTestData.States.Active,
        DateTimeOffset? courseUpdate = null,
        DateTimeOffset? itemCreation = null,
        DateTimeOffset? itemUpdate = null,
        DateTimeOffset? submissionUpdate = null)
    {
        var courseId = await CourseRows.InsertCourseAsync(
            host,
            cancellationToken,
            googleId: CourseTestData.CourseId(ordinal),
            name: CourseTestData.CourseName(ordinal),
            state: state,
            updateTime: courseUpdate);
        var participantId = await CourseRows.InsertParticipantAsync(
            host,
            cancellationToken,
            googleUserId: CourseTestData.UserId(ordinal),
            email: CourseTestData.Email($"member{ordinal}"),
            fullName: CourseTestData.Name(ordinal));
        await CourseRows.InsertMembershipAsync(host, courseId, participantId, cancellationToken, lastSeenAt: Now);
        var courseWorkId = await CourseWorkRows.InsertCourseWorkAsync(
            host,
            courseId,
            cancellationToken,
            googleId: CourseWorkTestData.ItemId(ordinal),
            creationTime: itemCreation,
            updateTime: itemUpdate);
        var submissionId = await CourseWorkRows.InsertSubmissionAsync(
            host,
            courseWorkId,
            participantId,
            cancellationToken,
            googleId: CourseWorkTestData.SubmissionId(ordinal),
            updateTime: submissionUpdate);

        // The rows' own created_at would otherwise be "now" and, for a course with no Google date at all, decide
        // its fate (OD-006); a course seeded with Google dates is judged by them alone.
        await host.ExecuteAsync(
            "UPDATE course SET created_at = @at, updated_at = @at WHERE id = @id",
            cancellationToken,
            ("at", Old),
            ("id", courseId));
        return new SeededCourse(courseId, courseWorkId, submissionId);
    }

    private static async Task<SeededMember> AddMemberAsync(
        InstallationTestHost host,
        SeededCourse course,
        int ordinal,
        CancellationToken cancellationToken,
        bool onRoster,
        DateTimeOffset lastSeen,
        bool withSubmission)
    {
        var participantId = await CourseRows.InsertParticipantAsync(
            host,
            cancellationToken,
            googleUserId: CourseTestData.UserId(ordinal),
            email: CourseTestData.Email($"member{ordinal}"),
            fullName: CourseTestData.Name(ordinal));
        await CourseRows.InsertMembershipAsync(
            host,
            course.CourseId,
            participantId,
            cancellationToken,
            firstSeenAt: lastSeen.AddDays(-100),
            lastSeenAt: lastSeen,
            onRoster: onRoster);
        long? submissionId = withSubmission
            ? await CourseWorkRows.InsertSubmissionAsync(
                host,
                course.CourseWorkId,
                participantId,
                cancellationToken,
                googleId: CourseWorkTestData.SubmissionId(ordinal),
                updateTime: Recent)
            : null;
        return new SeededMember(participantId, submissionId);
    }

    private static Task<long> MembershipCountAsync(
        InstallationTestHost host,
        long courseId,
        long participantId,
        CancellationToken cancellationToken) =>
        host.CountAsync(
            "course_membership",
            cancellationToken,
            "course_id = @course AND participant_id = @participant",
            ("course", courseId),
            ("participant", participantId));

    private static async Task<long> MembershipIdAsync(
        InstallationTestHost host,
        long courseId,
        long participantId,
        CancellationToken cancellationToken) =>
        await host.ScalarAsync<long>(
            "SELECT id FROM course_membership WHERE course_id = @course AND participant_id = @participant",
            cancellationToken,
            ("course", courseId),
            ("participant", participantId));

    private static async Task AssertCourseWholeAsync(InstallationTestHost host, SeededCourse course, CancellationToken cancellationToken)
    {
        Assert.Equal(1L, await host.CountAsync("course", cancellationToken, "id = @id", ("id", course.CourseId)));
        Assert.Equal(1L, await host.CountAsync("course_work", cancellationToken, "id = @id", ("id", course.CourseWorkId)));
        Assert.Equal(1L, await host.CountAsync("submission", cancellationToken, "id = @id", ("id", course.SubmissionId)));
        Assert.True(await host.CountAsync("course_membership", cancellationToken, "course_id = @id", ("id", course.CourseId)) >= 1);
    }

    private sealed record SeededCourse(long CourseId, long CourseWorkId, long SubmissionId);

    private sealed record SeededMember(long ParticipantId, long? SubmissionId);
}
