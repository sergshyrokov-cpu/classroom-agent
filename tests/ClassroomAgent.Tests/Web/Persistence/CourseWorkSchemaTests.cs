using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.Web.Persistence;

/// <summary>
/// US-015 db-design §3: the migration creates <c>course_work</c> — both Classroom resources in one table
/// (OD-008) — with the course-and-resource-scoped upsert key Specification v2 fixes, the two-code resource
/// vocabulary, and the material shape guard. Against real PostgreSQL via Testcontainers; the InMemory provider
/// enforces none of this (TC-2).
/// </summary>
/// <remarks>
/// US-015 OD-012: until <c>AddCourseWorkAndSubmissions</c> exists, every test here is expected to fail because the
/// table itself is absent, not because a constraint is wrong (test-strategy §9). <see cref="TheTable_Exists"/> is
/// asserted first for exactly that reason — an absence test against a table that does not exist would otherwise
/// "pass" inside that absence (test-strategy §9.2, the corrected US-014 <c>ClassroomParticipantSchemaTests</c>
/// precedent).
/// </remarks>
public sealed class CourseWorkSchemaTests(PostgreSqlFixture database)
{
    /// <summary>db-design §10 "table presence": asserted first, so the absence test below is not vacuous.</summary>
    [Fact]
    public async Task TheTable_Exists()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var tables = await host.TableNamesAsync(ct);

        Assert.Contains(tables, t => t == CourseWorkTestData.CourseWorkTable);
    }

    /// <summary>db-design §3.5, spec FR-010: the upsert key is scoped by course and resource.</summary>
    [Fact]
    public async Task DuplicateGoogleIdInTheSameCourseAndResource_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        await CourseWorkRows.InsertCourseWorkAsync(host, course, ct, googleId: CourseWorkTestData.ItemId(1));

        var second = async () => await CourseWorkRows.InsertCourseWorkAsync(
            host,
            course,
            ct,
            googleId: CourseWorkTestData.ItemId(1));

        var error = await Assert.ThrowsAsync<PostgresException>(second);
        Assert.Equal("23505", error.SqlState);
        Assert.Equal(CourseWorkTestData.Constraints.CourseWorkCourseResourceGoogleId, error.ConstraintName);
    }

    /// <summary>
    /// db-design §3.5, Specification v2's correction: the same item id in two different courses must succeed —
    /// Classroom documents <c>courseWork.id</c> as unique only per course.
    /// </summary>
    [Fact]
    public async Task TheSameGoogleIdInTwoCourses_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var first = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(1));
        var second = await CourseRows.InsertCourseAsync(
            host,
            ct,
            googleId: CourseTestData.CourseId(2),
            name: CourseTestData.CourseName(2));
        await CourseWorkRows.InsertCourseWorkAsync(host, first, ct, googleId: CourseWorkTestData.ItemId(1));

        var id = await CourseWorkRows.InsertCourseWorkAsync(host, second, ct, googleId: CourseWorkTestData.ItemId(1));

        Assert.True(id > 0);
    }

    /// <summary>db-design §3.5, PC-3: the same item id under both Classroom resources must succeed.</summary>
    [Fact]
    public async Task TheSameGoogleIdUnderBothResources_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        await CourseWorkRows.InsertCourseWorkAsync(
            host,
            course,
            ct,
            googleId: CourseWorkTestData.ItemId(1),
            resource: CourseWorkTestData.ResourceCodes.CourseWork);

        var id = await CourseWorkRows.InsertCourseWorkAsync(
            host,
            course,
            ct,
            googleId: CourseWorkTestData.ItemId(1),
            resource: CourseWorkTestData.ResourceCodes.CourseWorkMaterial,
            maxPoints: null,
            dueAt: null);

        Assert.True(id > 0);
    }

    /// <summary>db-design §3.2: a resource outside the two-code vocabulary is rejected.</summary>
    [Fact]
    public async Task ResourceOutsideTheVocabulary_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);

        var insert = async () => await CourseWorkRows.InsertCourseWorkAsync(host, course, ct, resource: "assignment");

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(CourseWorkTestData.Constraints.CourseWorkResource, error.ConstraintName);
    }

    /// <summary>
    /// db-design §3.1: <c>due_at</c>, <c>max_points</c>, <c>creation_time</c> and <c>update_time</c> are nullable
    /// on purpose — an item with none of them is storable. A later <c>NOT NULL</c> would break imports.
    /// </summary>
    [Fact]
    public async Task ItemWithoutOptionalGoogleTimes_IsStorable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);

        var id = await CourseWorkRows.InsertCourseWorkAsync(
            host,
            course,
            ct,
            dueAt: null,
            maxPoints: null,
            creationTime: null,
            updateTime: null);

        var stored = await host.QueryAsync(
            "SELECT due_at, max_points, creation_time, update_time FROM course_work WHERE id = @id",
            r => (HasDue: !r.IsDBNull(0), HasMax: !r.IsDBNull(1), HasCreation: !r.IsDBNull(2), HasUpdate: !r.IsDBNull(3)),
            ct,
            ("id", id));

        var row = Assert.Single(stored);
        Assert.False(row.HasDue);
        Assert.False(row.HasMax);
        Assert.False(row.HasCreation);
        Assert.False(row.HasUpdate);
    }

    /// <summary>db-design §4.4, PC-8: deleting a course that has coursework is refused, so the purge must delete explicitly.</summary>
    [Fact]
    public async Task DeletingACourseThatHasCourseWork_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        await CourseWorkRows.InsertCourseWorkAsync(host, course, ct);

        var delete = async () => await host.ExecuteAsync("DELETE FROM course WHERE id = @id", ct, ("id", course));

        var error = await Assert.ThrowsAsync<PostgresException>(delete);
        Assert.Equal("23503", error.SqlState);
        Assert.Equal(CourseWorkTestData.Constraints.CourseWorkCourseForeignKey, error.ConstraintName);
    }

    /// <summary>
    /// db-design §3.6, PC-3, FR-005: the graded/ungraded/material kind is derived and never stored — presence is
    /// asserted first so this absence is not vacuous.
    /// </summary>
    [Fact]
    public async Task TheTable_HasNoKindColumn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var tables = await host.TableNamesAsync(ct);
        Assert.Contains(tables, t => t == CourseWorkTestData.CourseWorkTable);

        var columns = await host.QueryAsync(
            "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @table",
            r => r.GetString(0),
            ct,
            ("table", CourseWorkTestData.CourseWorkTable));

        Assert.DoesNotContain(columns, c => c == "kind");
    }

    /// <summary>db-design §3.2, VR-001: data sanity on external input — a negative maximum is rejected.</summary>
    [Fact]
    public async Task NegativeMaxPoints_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);

        var insert = async () => await CourseWorkRows.InsertCourseWorkAsync(host, course, ct, maxPoints: -1m);

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(CourseWorkTestData.Constraints.CourseWorkMaxPointsNonNegative, error.ConstraintName);
    }

    /// <summary>
    /// db-design §3.2, OD-008: a material has no submissions, no points and no due date — either field alone is
    /// enough to violate <c>ck_course_work_material_has_no_grading</c>.
    /// </summary>
    [Theory]
    [InlineData(100, null)]
    [InlineData(null, "2026-10-01T00:00:00Z")]
    public async Task MaterialWithMaxPointsOrDueDate_IsRejected(int? maxPoints, string? dueAt)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);

        var insert = async () => await CourseWorkRows.InsertCourseWorkAsync(
            host,
            course,
            ct,
            resource: CourseWorkTestData.ResourceCodes.CourseWorkMaterial,
            maxPoints: maxPoints.HasValue ? (decimal?)maxPoints.Value : null,
            dueAt: dueAt is null
                ? null
                : DateTimeOffset.Parse(dueAt, System.Globalization.CultureInfo.InvariantCulture));

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(CourseWorkTestData.Constraints.CourseWorkMaterialHasNoGrading, error.ConstraintName);
    }
}
