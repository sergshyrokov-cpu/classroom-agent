using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using static ClassroomAgent.Tests.TestInfrastructure.DataHealthHostExtensions;
using Cause = ClassroomAgent.Tests.TestInfrastructure.ReadOnlyModeHost.Cause;
using LegitimacyModeReason = ClassroomAgent.Application.Models.LegitimacyModeReason;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-062 FR-001, FR-002, FR-004 in the Application layer (TC-1, spec §7 "enforced in the Application query result"):
/// <see cref="GetDataHealthQuery"/> and <see cref="GetDataHealthCountsQuery"/> resolved from a running installation over
/// real PostgreSQL (TC-2), so the problems are computed from stored data through the real fact queries. The stored facts
/// are written straight into their columns (<see cref="DataHealthHostExtensions"/>); the clock is the host's manual one
/// (TC-4); every name is invented.
/// </summary>
public sealed class DataHealthQueryTests(PostgreSqlFixture database)
{
    /// <summary>A night instant: its Kyiv date is the next day, so a UTC mix-up fails (TC-8).</summary>
    private static readonly DateTimeOffset D1 = new(2026, 9, 10, 21, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset D2 = new(2026, 9, 12, 22, 15, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset D3 = new(2026, 9, 5, 23, 45, 0, TimeSpan.Zero);

    private static async Task<InstallationTestHost> StartAsync(PostgreSqlFixture database, CancellationToken ct, Cause cause = Cause.NotReadOnly)
    {
        var host = await ReadOnlyModeHost.StartAsync(database, cause, ct);
        await host.ReadyToSeedAsync(ct);
        return host;
    }

    private static async Task<DataHealthPageModel> PageAsync(InstallationTestHost host, AppRole role, CancellationToken ct)
    {
        using var scope = host.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<GetDataHealthQuery>().ExecuteAsync(role, ct);
    }

    private static async Task<DataHealthBannerModel?> CountsAsync(InstallationTestHost host, AppRole role, CancellationToken ct)
    {
        using var scope = host.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<GetDataHealthCountsQuery>().ExecuteAsync(role, ct);
    }

    private static IReadOnlyList<TechnicalProblemKind> Kinds(DataHealthPageModel page) => page.Technical.Select(t => t.Kind).ToList();

    private static CourseProblems Of(IReadOnlyList<CourseProblems> list, string name) =>
        Assert.Single(list, c => c.Course.Name == name);

    /// <summary>FR-001.5 over the page: one per course × teaching kind present.</summary>
    private static int TeachingCount(IEnumerable<CourseProblems> courses) =>
        courses.Sum(c =>
            (c.NotInClassroomSince is null ? 0 : 1)
            + (c.Skipped is null ? 0 : 1)
            + (c.ItemsNotInClassroom.Count == 0 ? 0 : 1)
            + (c.AboveMaximum.Count == 0 ? 0 : 1));

    // ---- No problem (AC-002, FR-004.2) ------------------------------------------------------------------------------

    /// <summary>AC-002, FR-003.1, FR-004.2: with no condition holding, the banner model is null and the page says so.</summary>
    [Theory]
    [InlineData(AppRole.Dean)]
    [InlineData(AppRole.Admin)]
    public async Task WithNoProblem_TheBannerIsNull_AndThePageSaysSo(AppRole role)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        await host.RecentSuccessAsync(ct);
        await host.CourseAsync(1, "Test Healthy Course", ct);

        Assert.Null(await CountsAsync(host, role, ct));
        var page = await PageAsync(host, role, ct);
        Assert.True(page.HasNoProblem);
        Assert.Empty(page.Technical);
        Assert.Empty(page.Teaching);
        Assert.Empty(page.Archived);
        Assert.Null(page.ReadOnly);
        Assert.Equal(role == AppRole.Admin, page.IsAdmin);
    }

    // ---- Counting (AC-001, AC-003, FR-001.5) ------------------------------------------------------------------------

    /// <summary>
    /// AC-001, FR-001.5: two above-maximum grades on different items and an item "not in Classroom" on course A, course B
    /// "not in Classroom", and the last run failed (its last success within 24 hours, so no X2) — 1 technical, 3 teaching.
    /// </summary>
    [Theory]
    [InlineData(AppRole.Dean)]
    [InlineData(AppRole.Admin)]
    public async Task TheAc001Data_CountsOneTechnicalAndThreeTeaching(AppRole role)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        var now = host.Time.GetUtcNow();
        await host.SyncStateAsync(
            ct, status: "failed", lastError: "GoogleUnavailable", failedStep: "classroom", lastSuccessfulRunAt: now.AddHours(-3));
        var a = await host.CourseAsync(1, "Test Course A", ct);
        await host.GradedAsync(a, 1, "Test Item One", 10m, "Overone", 11m, null, ct);
        await host.GradedAsync(a, 2, "Test Item Two", 5m, "Overtwo", 6m, null, ct);
        await host.ItemNotInClassroomAsync(a, 3, "Test Item Gone", D1, ct);
        var b = await host.CourseAsync(2, "Test Course B", ct);
        await host.MarkCourseAsync(b, "not_in_classroom", D2, ct);

        Assert.Equal(new DataHealthBannerModel(1, 3), await CountsAsync(host, role, ct));
        var page = await PageAsync(host, role, ct);
        Assert.Equal([TechnicalProblemKind.LastRunFailed], Kinds(page));
        Assert.Equal(2, Of(page.Teaching, "Test Course A").AboveMaximum.Count);
        Assert.Single(Of(page.Teaching, "Test Course A").ItemsNotInClassroom);
        Assert.Equal(D2, Of(page.Teaching, "Test Course B").NotInClassroomSince);
        Assert.False(page.HasNoProblem);
    }

    /// <summary>
    /// AC-003, FR-001.5, FR-004.4: an archived course whose only problem is an above-maximum grade — no count, and the page
    /// lists it apart, with the grade's details.
    /// </summary>
    [Fact]
    public async Task AnArchivedCourse_IsListedApart_WithItsDetails_AndNotCounted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        await host.RecentSuccessAsync(ct);
        var archived = await host.CourseAsync(1, "Test Archived Course", ct, state: CourseTestData.States.Archived);
        await host.GradedAsync(archived, 1, "Test Archived Item", 10m, "Archivedstudent", 14m, null, ct);

        Assert.Null(await CountsAsync(host, AppRole.Dean, ct));
        var page = await PageAsync(host, AppRole.Dean, ct);
        Assert.Empty(page.Teaching);
        var entry = Of(page.Archived, "Test Archived Course");
        var grade = Assert.Single(entry.AboveMaximum);
        Assert.Equal(("Test Archived Item", 14m, 10m, false), (grade.ItemTitle, grade.Points, grade.Maximum, grade.IsDraft));
        Assert.Contains("Archivedstudent", grade.StudentName, StringComparison.Ordinal);
    }

    /// <summary>AC-004, FR-001.4: the stored grade corrected to the maximum — the problem is gone from both at once.</summary>
    [Fact]
    public async Task ACorrectedGrade_RemovesTheProblem_AtTheNextRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        await host.RecentSuccessAsync(ct);
        var course = await host.CourseAsync(1, "Test Corrected Course", ct);
        var submission = await host.GradedAsync(course, 1, "Test Item", 10m, "Corrected", 12m, null, ct);
        Assert.Equal(new DataHealthBannerModel(0, 1), await CountsAsync(host, AppRole.Dean, ct));

        // What the next run stores for the corrected grade (the run's own write path is US-055 / US-059's).
        await host.ExecuteAsync("UPDATE submission SET assigned_grade = 10 WHERE id = @id", ct, ("id", submission));

        Assert.Null(await CountsAsync(host, AppRole.Dean, ct));
        var page = await PageAsync(host, AppRole.Dean, ct);
        Assert.Empty(page.Teaching);
        Assert.True(page.HasNoProblem);
    }

    // ---- Teaching details (AC-005, FR-001.2, FR-001.6, FR-004.3) ----------------------------------------------------

    /// <summary>
    /// AC-005, FR-001.2, FR-001.6, OD-003, I-10: every teaching kind with its details — the above-maximum grade and draft,
    /// the item gone, T3 of a loaded course with and without a last complete read, T3 of a never-loaded course by its
    /// stored name and, without one, by its Google id, and T4 — journal links for loaded courses only, and the count.
    /// </summary>
    [Fact]
    public async Task EveryTeachingKind_CarriesItsDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        await host.RecentSuccessAsync(ct);
        var graded = await host.CourseAsync(1, "Test Graded Course", ct);
        await host.GradedAsync(graded, 1, "Test Graded Item", 10m, "Overmax", 12m, 13m, ct);
        await host.ItemNotInClassroomAsync(graded, 2, "Test Gone Item", D1, ct);
        var since = await host.CourseAsync(2, "Test Skipped Since", ct, lastCompleteReadAt: D2);
        await host.SkipAsync(CourseTestData.CourseId(2), "Test Skipped Since", ValueOutOfRangeReason, D3, ct);
        var noDate = await host.CourseAsync(3, "Test Skipped No Date", ct);
        await host.SkipAsync(CourseTestData.CourseId(3), "Test Skipped No Date", ValueOutOfRangeReason, D3, ct);
        await host.SkipAsync(CourseTestData.CourseId(4), "Test Never Loaded", ValueOutOfRangeReason, D3, ct);
        await host.SkipAsync(CourseTestData.CourseId(5), null, ValueOutOfRangeReason, D3, ct);
        var absent = await host.CourseAsync(6, "Test Absent Course", ct);
        await host.MarkCourseAsync(absent, "not_in_classroom", D2, ct);

        var page = await PageAsync(host, AppRole.Dean, ct);

        var g = Of(page.Teaching, "Test Graded Course");
        Assert.Equal($"/workspace/journal?courseId={graded}", g.JournalPath);
        Assert.Equal(graded, g.Course.CourseId);
        Assert.Equal(
            new[] { ("Test Graded Item", 12m, 10m, false), ("Test Graded Item", 13m, 10m, true) },
            g.AboveMaximum.Select(r => (r.ItemTitle, r.Points, r.Maximum, r.IsDraft)).OrderBy(r => r.Item4));
        Assert.All(g.AboveMaximum, r => Assert.Contains("Overmax", r.StudentName, StringComparison.Ordinal));
        var item = Assert.Single(g.ItemsNotInClassroom);
        Assert.Equal(("Test Gone Item", D1), (item.Title, item.Since));
        Assert.Null(g.Skipped);
        Assert.Null(g.NotInClassroomSince);

        var s = Of(page.Teaching, "Test Skipped Since");
        Assert.Equal(new SkippedCourse(SkippedCourseVariant.Since, D2, null), s.Skipped);
        Assert.Equal($"/workspace/journal?courseId={since}", s.JournalPath);
        Assert.Equal(SkippedCourseVariant.NoDate, Of(page.Teaching, "Test Skipped No Date").Skipped!.Variant);
        Assert.Equal($"/workspace/journal?courseId={noDate}", Of(page.Teaching, "Test Skipped No Date").JournalPath);

        var never = Of(page.Teaching, "Test Never Loaded");
        Assert.Equal(new SkippedCourse(SkippedCourseVariant.NeverLoaded, null, D3), never.Skipped);
        Assert.Null(never.JournalPath);
        Assert.Null(never.Course.CourseId);
        var untitled = Assert.Single(page.Teaching, c => c.Course.GoogleCourseId == CourseTestData.CourseId(5));
        Assert.Null(untitled.Course.Name);
        Assert.Null(untitled.JournalPath);
        Assert.Equal(SkippedCourseVariant.NeverLoaded, untitled.Skipped!.Variant);

        Assert.Equal(D2, Of(page.Teaching, "Test Absent Course").NotInClassroomSince);
        Assert.Equal($"/workspace/journal?courseId={absent}", Of(page.Teaching, "Test Absent Course").JournalPath);

        // Graded: T1 + T2; Since, No Date, Never Loaded, Untitled: T3 each; Absent: T4.
        Assert.Equal(7, TeachingCount(page.Teaching));
        Assert.Equal(new DataHealthBannerModel(0, 7), await CountsAsync(host, AppRole.Dean, ct));
        Assert.Empty(page.Technical);
    }

    /// <summary>FR-004.3: courses in the culture order of their names — «Ґ» after «Г» and before «І», unlike ordinal order.</summary>
    [Fact]
    public async Task TeachingCourses_AreInCultureOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        await host.RecentSuccessAsync(ct);
        string[] names = ["Історія Тест", "Ґрунтознавство Тест", "Алгебра Тест"];
        for (var i = 0; i < names.Length; i++)
        {
            var id = await host.CourseAsync(i + 1, names[i], ct);
            await host.MarkCourseAsync(id, "not_in_classroom", D1, ct);
        }

        var page = await PageAsync(host, AppRole.Dean, ct);

        Assert.Equal(["Алгебра Тест", "Ґрунтознавство Тест", "Історія Тест"], page.Teaching.Select(c => c.Course.Name));
    }

    // ---- Technical problems and the roles (AC-006, FR-001.3, FR-002.3) -----------------------------------------------

    /// <summary>
    /// AC-006, FR-002.3, I-2, spec §7: the last run failed with a permission error and a course is skipped for an unknown
    /// state — the Dean's model carries both titles and no details at all; the Admin's carries the diagnosis, the failed
    /// step, the finish and the course with its date.
    /// </summary>
    [Fact]
    public async Task TechnicalProblems_ReachTheDeanWithoutDetails_AndTheAdminWithThem()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        var now = host.Time.GetUtcNow();
        var started = now.AddHours(-2);
        await host.SyncStateAsync(
            ct, status: "failed", startedAt: started, lastError: "ScopeNotAuthorized", failedStep: "classroom",
            lastSuccessfulRunAt: now.AddHours(-5));
        await host.SkipAsync(CourseTestData.CourseId(7), "Test Unknown State Course", UnknownStateReason, D1, ct);

        var dean = await PageAsync(host, AppRole.Dean, ct);
        var admin = await PageAsync(host, AppRole.Admin, ct);

        Assert.Equal([TechnicalProblemKind.LastRunFailed, TechnicalProblemKind.UnknownCourseState], Kinds(dean));
        Assert.All(dean.Technical, t => Assert.Null(t.Details));
        Assert.Empty(dean.Teaching);
        Assert.Equal(Kinds(dean), Kinds(admin));
        var failed = admin.Technical[0].Details!;
        Assert.Equal("ScopeNotAuthorized", failed.Diagnosis);
        Assert.NotNull(failed.FailedStep);
        Assert.Equal(started.AddMinutes(5), failed.FinishedAt);
        var unknown = admin.Technical[1].Details!;
        Assert.Equal("Test Unknown State Course", unknown.Course!.Name);
        Assert.Equal(D1, unknown.SkippedSince);
        Assert.Equal(new DataHealthBannerModel(2, 0), await CountsAsync(host, AppRole.Dean, ct));
    }

    /// <summary>VR-004: a stored error that names no known diagnosis is shown as <c>Unexpected</c>.</summary>
    [Fact]
    public async Task AnUnknownStoredError_IsReportedAsUnexpected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        await host.SyncStateAsync(
            ct, status: "failed", lastError: "A legacy error text", lastSuccessfulRunAt: host.Time.GetUtcNow().AddHours(-2));

        var admin = await PageAsync(host, AppRole.Admin, ct);

        Assert.Equal("Unexpected", Assert.Single(admin.Technical).Details!.Diagnosis);
    }

    /// <summary>I-1: while the next run is in progress the stored error is cleared and X1 is not raised.</summary>
    [Fact]
    public async Task ARunInProgress_RaisesNoFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        await host.SyncStateAsync(ct, status: "running", lastSuccessfulRunAt: host.Time.GetUtcNow().AddHours(-2));

        Assert.Null(await CountsAsync(host, AppRole.Admin, ct));
    }

    /// <summary>AC-007, FR-001.3 X2: a last success 25 hours ago raises X2 with its time; 24 hours exactly and 23 do not.</summary>
    [Theory]
    [InlineData(25, true)]
    [InlineData(24, false)]
    [InlineData(23, false)]
    public async Task NoRecentSuccess_IsRaisedStrictlyAfter24Hours(int hoursAgo, bool raised)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        var success = host.Time.GetUtcNow().AddHours(-hoursAgo);
        await host.SyncStateAsync(ct, startedAt: success.AddMinutes(-5), lastSuccessfulRunAt: success);

        var admin = await PageAsync(host, AppRole.Admin, ct);

        if (raised)
        {
            var problem = Assert.Single(admin.Technical);
            Assert.Equal(TechnicalProblemKind.NoRecentSuccess, problem.Kind);
            Assert.Equal(success, problem.Details!.LastSuccessAt);
            Assert.Equal(new DataHealthBannerModel(1, 0), await CountsAsync(host, AppRole.Dean, ct));
        }
        else
        {
            Assert.Empty(admin.Technical);
        }
    }

    /// <summary>AC-007, FR-001.3 X2: no successful run ever — with no state row, or a row without a success — is "none yet".</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoSuccessEver_IsNoneYet(bool withFailedRow)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        if (withFailedRow)
        {
            await host.SyncStateAsync(ct, status: "failed", lastError: "GoogleUnavailable");
        }

        var dean = await PageAsync(host, AppRole.Dean, ct);

        Assert.Contains(TechnicalProblemKind.NoSuccessYet, Kinds(dean));
        Assert.DoesNotContain(TechnicalProblemKind.NoRecentSuccess, Kinds(dean));
    }

    /// <summary>AC-007, AC-014, FR-001.3: in read-only mode, for any cause, neither X2 nor X6 is raised.</summary>
    [Theory]
    [InlineData(Cause.NeverConfirmed)]
    [InlineData(Cause.Suspended)]
    [InlineData(Cause.GracePeriodExpired)]
    public async Task InReadOnlyMode_NeitherStalenessNorTheControlPlaneProblemIsRaised(Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct, cause);

        var admin = await PageAsync(host, AppRole.Admin, ct);

        Assert.DoesNotContain(TechnicalProblemKind.NoSuccessYet, Kinds(admin));
        Assert.DoesNotContain(TechnicalProblemKind.NoRecentSuccess, Kinds(admin));
        Assert.DoesNotContain(TechnicalProblemKind.ControlPlaneUnreachable, Kinds(admin));
        Assert.Null(await CountsAsync(host, AppRole.Admin, ct));
    }

    /// <summary>FR-004.5, I-3: the read-only notice names the current reason; outside read-only mode there is none.</summary>
    [Theory]
    [InlineData(Cause.NeverConfirmed, LegitimacyModeReason.NotYetConfirmed)]
    [InlineData(Cause.Suspended, LegitimacyModeReason.SuspendedByOwner)]
    [InlineData(Cause.GracePeriodExpired, LegitimacyModeReason.GracePeriodExpired)]
    public async Task InReadOnlyMode_ThePageCarriesTheReadOnlyNotice(Cause cause, LegitimacyModeReason reason)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct, cause);

        var dean = await PageAsync(host, AppRole.Dean, ct);

        Assert.Equal(new ReadOnlyNotice(reason), dean.ReadOnly);
    }

    /// <summary>AC-008, FR-001.3 X4, FR-008.3: the stored empty-list date is one problem for both roles, with its date.</summary>
    [Theory]
    [InlineData(AppRole.Dean)]
    [InlineData(AppRole.Admin)]
    public async Task AnEmptyCourseListSince_IsOneProblemWithItsDate(AppRole role)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        var success = host.Time.GetUtcNow().AddHours(-1);
        await host.SyncStateAsync(ct, startedAt: success.AddMinutes(-5), lastSuccessfulRunAt: success, emptySince: D1);

        var page = await PageAsync(host, role, ct);

        var problem = Assert.Single(page.Technical);
        Assert.Equal(TechnicalProblemKind.CourseListEmpty, problem.Kind);
        Assert.Equal(D1, problem.EmptySince);
        Assert.Equal(new DataHealthBannerModel(1, 0), await CountsAsync(host, role, ct));

        await host.ExecuteAsync("UPDATE sync_state SET last_course_list_empty = false, course_list_empty_since = NULL", ct);
        Assert.Null(await CountsAsync(host, role, ct));
    }

    /// <summary>
    /// AC-009, FR-001.3 X5, FR-001.5, FR-004.4: three courses "not visible to the technical account", one archived — one
    /// problem counted once; the Admin's details list all three, the Dean's none; the archived one is not repeated in the
    /// archived section.
    /// </summary>
    [Fact]
    public async Task CoursesNotVisible_AreOneProblem_ListedForTheAdminOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        await host.RecentSuccessAsync(ct);
        var ids = new List<long>();
        for (var i = 1; i <= 3; i++)
        {
            var id = await host.CourseAsync(
                i, $"Test Hidden {i}", ct, state: i == 3 ? CourseTestData.States.Archived : CourseTestData.States.Active,
                lastCompleteReadAt: D3);
            await host.MarkCourseAsync(id, "not_visible_to_technical_account", D1, ct);
            ids.Add(id);
        }

        var admin = await PageAsync(host, AppRole.Admin, ct);
        var dean = await PageAsync(host, AppRole.Dean, ct);

        var problem = Assert.Single(admin.Technical);
        Assert.Equal(TechnicalProblemKind.CoursesNotVisible, problem.Kind);
        var listed = problem.Details!.NotVisibleCourses!;
        Assert.Equal(ids.Order(), listed.Select(c => c.CourseId).Order());
        Assert.All(listed, c => Assert.Equal((D1, (DateTimeOffset?)D3), (c.MarkedSince, c.LastCompleteReadAt)));
        Assert.Equal("Test Hidden 1", Assert.Single(listed, c => c.CourseId == ids[0]).Name);
        Assert.Equal(TechnicalProblemKind.CoursesNotVisible, Assert.Single(dean.Technical).Kind);
        Assert.Null(dean.Technical[0].Details);
        Assert.Empty(admin.Archived);
        Assert.Empty(admin.Teaching);
        Assert.Equal(new DataHealthBannerModel(1, 0), await CountsAsync(host, AppRole.Dean, ct));
    }

    /// <summary>
    /// AC-013, FR-001.3 X6, I-2: the last successful legitimacy check C1 25 hours ago — one problem for both roles with C1
    /// and C1 + 7 days, no details for the Dean; 24 hours exactly and 23 hours raise nothing.
    /// </summary>
    [Theory]
    [InlineData(25, true)]
    [InlineData(24, false)]
    [InlineData(23, false)]
    public async Task NoConnectionWithTheControlPlane_IsRaisedStrictlyAfter24Hours(int hoursAgo, bool raised)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        await host.RecentSuccessAsync(ct);
        var c1 = host.Time.GetUtcNow().AddHours(-hoursAgo);
        await host.LastCheckAsync(c1, ct);

        var dean = await PageAsync(host, AppRole.Dean, ct);
        var admin = await PageAsync(host, AppRole.Admin, ct);

        if (!raised)
        {
            Assert.Empty(dean.Technical);
            Assert.Null(await CountsAsync(host, AppRole.Dean, ct));
            return;
        }

        foreach (var page in new[] { dean, admin })
        {
            var problem = Assert.Single(page.Technical);
            Assert.Equal(TechnicalProblemKind.ControlPlaneUnreachable, problem.Kind);
            Assert.Equal(c1, problem.LastCheckAt);
            Assert.Equal(c1.AddDays(7), problem.ReadOnlyFrom);
        }

        Assert.Null(dean.Technical[0].Details);
        Assert.False(admin.Technical[0].Details!.UpgradeRequired ?? false);
        Assert.Null(dean.ReadOnly);
        Assert.Equal(new DataHealthBannerModel(1, 0), await CountsAsync(host, AppRole.Dean, ct));
    }

    /// <summary>AC-014, I-9, BR-027: with <c>upgrade_required</c> as the cause during the grace period the Admin's details name it.</summary>
    [Fact]
    public async Task UpgradeRequired_IsNamedInTheAdminsDetails_Only()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        await host.RecentSuccessAsync(ct);
        await host.LastCheckAsync(host.Time.GetUtcNow().AddHours(-30), ct, compatibility: "upgrade_required");

        var admin = await PageAsync(host, AppRole.Admin, ct);
        var dean = await PageAsync(host, AppRole.Dean, ct);

        Assert.True(Assert.Single(admin.Technical).Details!.UpgradeRequired);
        Assert.Null(Assert.Single(dean.Technical).Details);
    }

    /// <summary>I-9: <c>upgrade_required</c> within 24 hours of the last success raises no problem of its own.</summary>
    [Fact]
    public async Task UpgradeRequired_WithinADay_RaisesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        await host.RecentSuccessAsync(ct);
        await host.LastCheckAsync(host.Time.GetUtcNow().AddHours(-2), ct, compatibility: "upgrade_required");

        Assert.Null(await CountsAsync(host, AppRole.Admin, ct));
    }

    /// <summary>
    /// Api-design §2.10: technical problems in the order X1, X2, X6, X4, X5, then X3 per course in culture order.
    /// </summary>
    [Fact]
    public async Task TechnicalProblems_AreInTheDesignedOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        var now = host.Time.GetUtcNow();
        await host.SyncStateAsync(
            ct, status: "failed", lastError: "GoogleUnavailable", lastSuccessfulRunAt: now.AddHours(-30), emptySince: D1);
        await host.LastCheckAsync(now.AddHours(-30), ct);
        var hidden = await host.CourseAsync(1, "Test Hidden", ct);
        await host.MarkCourseAsync(hidden, "not_visible_to_technical_account", D1, ct);
        await host.SkipAsync(CourseTestData.CourseId(2), "Історія Тест", UnknownStateReason, D1, ct);
        await host.SkipAsync(CourseTestData.CourseId(3), "Алгебра Тест", UnknownStateReason, D1, ct);

        var admin = await PageAsync(host, AppRole.Admin, ct);

        Assert.Equal(
            [
                TechnicalProblemKind.LastRunFailed, TechnicalProblemKind.NoRecentSuccess,
                TechnicalProblemKind.ControlPlaneUnreachable, TechnicalProblemKind.CourseListEmpty,
                TechnicalProblemKind.CoursesNotVisible, TechnicalProblemKind.UnknownCourseState,
                TechnicalProblemKind.UnknownCourseState,
            ],
            Kinds(admin));
        Assert.Equal(["Алгебра Тест", "Історія Тест"], admin.Technical.Skip(5).Select(t => t.Details!.Course!.Name));
        Assert.Equal(new DataHealthBannerModel(7, 0), await CountsAsync(host, AppRole.Admin, ct));
    }

    /// <summary>
    /// FR-001.5, FR-004.4, OD-004 a: an archived course skipped for an unknown state is listed in the archived section and
    /// not counted; the Dean's entry has no course name and no journal link, the Admin's has both.
    /// </summary>
    [Fact]
    public async Task AnArchivedUnknownStateCourse_IsListedApart_WithoutItsNameForTheDean()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        await host.RecentSuccessAsync(ct);
        var archived = await host.CourseAsync(1, "Test Archived Unknown", ct, state: CourseTestData.States.Archived);
        await host.SkipAsync(CourseTestData.CourseId(1), "Test Archived Unknown", UnknownStateReason, D1, ct);

        var dean = await PageAsync(host, AppRole.Dean, ct);
        var admin = await PageAsync(host, AppRole.Admin, ct);

        Assert.Empty(dean.Technical);
        var deanEntry = Assert.Single(dean.Archived);
        Assert.True(deanEntry.UnknownState);
        Assert.Null(deanEntry.Course.Name);
        Assert.Null(deanEntry.JournalPath);
        var adminEntry = Assert.Single(admin.Archived);
        Assert.True(adminEntry.UnknownState);
        Assert.Equal("Test Archived Unknown", adminEntry.Course.Name);
        Assert.Equal($"/workspace/journal?courseId={archived}", adminEntry.JournalPath);
        Assert.Null(await CountsAsync(host, AppRole.Admin, ct));
    }

    /// <summary>
    /// Api-design §2.3, FR-001.5: one fixture with every kind — archived and X5 included — gives the banner exactly the
    /// totals the page lists, for both roles.
    /// </summary>
    [Theory]
    [InlineData(AppRole.Dean)]
    [InlineData(AppRole.Admin)]
    public async Task TheBannerCounts_EqualThePageTotals(AppRole role)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        var now = host.Time.GetUtcNow();
        await host.SyncStateAsync(ct, status: "failed", lastError: "KeyRejected", lastSuccessfulRunAt: now.AddHours(-26));
        await host.LastCheckAsync(now.AddHours(-26), ct);
        var a = await host.CourseAsync(1, "Test Mixed A", ct);
        await host.GradedAsync(a, 1, "Test Item A", 10m, "Mixeda", 11m, null, ct);
        await host.ItemNotInClassroomAsync(a, 2, "Test Gone A", D1, ct);
        await host.MarkCourseAsync(a, "not_in_classroom", D2, ct);
        var b = await host.CourseAsync(2, "Test Mixed B", ct, lastCompleteReadAt: D3);
        await host.SkipAsync(CourseTestData.CourseId(2), "Test Mixed B", ValueOutOfRangeReason, D1, ct);
        var archived = await host.CourseAsync(3, "Test Mixed Archived", ct, state: CourseTestData.States.Archived);
        await host.GradedAsync(archived, 3, "Test Item Archived", 1m, "Mixedc", 2m, null, ct);
        await host.MarkCourseAsync(archived, "not_in_classroom", D2, ct);
        for (var i = 4; i <= 5; i++)
        {
            var hidden = await host.CourseAsync(
                i, $"Test Mixed Hidden {i}", ct, state: i == 5 ? CourseTestData.States.Archived : CourseTestData.States.Active);
            await host.MarkCourseAsync(hidden, "not_visible_to_technical_account", D1, ct);
        }

        await host.SkipAsync(CourseTestData.CourseId(8), "Test Mixed Unknown", UnknownStateReason, D1, ct);
        await host.SkipAsync(CourseTestData.CourseId(9), null, ValueOutOfRangeReason, D1, ct);

        var page = await PageAsync(host, role, ct);
        var counts = await CountsAsync(host, role, ct);

        // X1, X2, X6, X5, X3 = 5 technical; A: T4 + T2 + T1, B: T3, never-loaded: T3 = 5 teaching; archived not counted.
        Assert.Equal(new DataHealthBannerModel(page.Technical.Count, TeachingCount(page.Teaching)), counts);
        Assert.Equal(new DataHealthBannerModel(5, 5), counts);
        Assert.Single(page.Archived);
    }

    /// <summary>FR-001.1, SC-8: computing the banner and the page writes nothing and calls no Google port.</summary>
    [Fact]
    public async Task TheQueries_WriteNothing_AndCallNoGoogle()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(database, ct);
        await host.SyncStateAsync(ct, status: "failed", lastError: "GoogleUnavailable");
        var course = await host.CourseAsync(1, "Test Quiet Course", ct);
        await host.GradedAsync(course, 1, "Test Item", 10m, "Quiet", 11m, null, ct);
        await host.MarkCourseAsync(course, "not_visible_to_technical_account", D1, ct);
        var teaching = await host.TeachingFingerprintAsync(ct);
        var state = await host.QueryAsync("SELECT row_to_json(s)::text FROM sync_state s", r => r.GetString(0), ct);
        var audit = await host.AuditRowsAsync(ct);

        await PageAsync(host, AppRole.Admin, ct);
        await PageAsync(host, AppRole.Dean, ct);
        await CountsAsync(host, AppRole.Admin, ct);

        Assert.Equal(teaching, await host.TeachingFingerprintAsync(ct));
        Assert.Equal(state, await host.QueryAsync("SELECT row_to_json(s)::text FROM sync_state s", r => r.GetString(0), ct));
        Assert.Equal(audit, await host.AuditRowsAsync(ct));
        Assert.Equal(0, host.GoogleCallCount());
    }
}
