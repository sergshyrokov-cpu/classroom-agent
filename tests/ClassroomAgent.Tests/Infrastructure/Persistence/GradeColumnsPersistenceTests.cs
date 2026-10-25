using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-055 db-design §3 … §7 against real PostgreSQL (TC-2): the migration <c>WidenGradeColumns</c>, the unconstrained
/// <c>numeric</c> type of <c>course_work.max_points</c>, <c>submission.assigned_grade</c> and
/// <c>submission.draft_grade</c>, the values it keeps (AC-005), the values the columns now hold exactly (AC-001 … AC-003,
/// VR-002) and the CHECK constraints it keeps. All values are synthetic (TC-4, S-03).
/// </summary>
public sealed class GradeColumnsPersistenceTests(PostgreSqlFixture database)
{
    /// <summary>The last migration before this Story — the schema every existing installation has (spec AC-005).</summary>
    private const string BeforeThisStory = "20261015203416_AddNotAnAdminAccountRefusal";

    private const string MigrationSuffix = "_WidenGradeColumns";

    /// <summary>The old column's ceiling, <c>numeric(10,4)</c>.</summary>
    private const decimal OldMaximum = 999_999.9999m;

    private static readonly (string Table, string Column)[] GradeColumns =
    [
        ("course_work", "max_points"), ("submission", "assigned_grade"), ("submission", "draft_grade"),
    ];

    /// <summary>The production options (snake_case naming, PC-5), as the other persistence tests build them.</summary>
    private static ClassroomAgentDbContext Context(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<ClassroomAgentDbContext>();
        ClassroomAgentDbContextOptions.Configure(builder, connectionString);
        builder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)); // as InstallationTestHost migrates
        return new ClassroomAgentDbContext(builder.Options);
    }

    /// <summary>Db-design §3.2, FR-003: the Story ships exactly one migration under the designed name, after the last one.</summary>
    [Fact]
    public async Task TheMigration_ExistsUnderTheDesignedName_AfterTheLastOne()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var db = Context(host.ConnectionString);

        var migrations = db.Database.GetMigrations().ToList();

        var ours = Assert.Single(migrations, m => m.EndsWith(MigrationSuffix, StringComparison.Ordinal));
        Assert.True(string.CompareOrdinal(ours, BeforeThisStory) > 0);
        Assert.Contains(BeforeThisStory, migrations);
    }

    /// <summary>
    /// Db-design §3.1, §7, FR-001: after all migrations the three columns are <c>numeric</c> with neither precision nor
    /// scale, and stay nullable (FR-002).
    /// </summary>
    [Fact]
    public async Task TheThreeColumns_AreUnconstrainedNumeric_AndNullable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        foreach (var (table, column) in GradeColumns)
        {
            var shape = await host.QueryAsync(
                """
                SELECT data_type || '|' || coalesce(numeric_precision::text, '-') || '|' || coalesce(numeric_scale::text, '-')
                       || '|' || is_nullable
                FROM information_schema.columns
                WHERE table_schema = current_schema() AND table_name = @table AND column_name = @column
                """,
                r => r.GetString(0),
                ct,
                ("table", table),
                ("column", column));
            Assert.Equal(["numeric|-|-|YES"], shape);
        }
    }

    /// <summary>
    /// AC-005, FR-003, db-design §3.2, §7: rows stored at the schema before this Story — four-decimal values, the old
    /// maximum, zero and nulls — read exactly the same text after <c>WidenGradeColumns</c> is applied.
    /// </summary>
    [Fact]
    public async Task TheMigration_KeepsEveryStoredValueAndNull()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct, migrate: false);
        await using (var db = Context(host.ConnectionString))
        {
            // Without the migration the steps below would migrate nothing and compare a table with itself.
            Assert.Single(db.Database.GetMigrations(), m => m.EndsWith(MigrationSuffix, StringComparison.Ordinal));
            await db.GetService<IMigrator>().MigrateAsync(BeforeThisStory, ct);
        }

        var course = await CourseRows.InsertCourseAsync(host, ct);
        var participant = await CourseRows.InsertParticipantAsync(host, ct);
        decimal?[] maxima = [OldMaximum, 12.3456m, 0m, 7.5m, null];
        decimal?[] assigned = [OldMaximum, 0.0001m, null, 3m, 0m];
        decimal?[] drafts = [null, 6.25m, 999_999m, 12.3456m, OldMaximum];
        for (var i = 0; i < maxima.Length; i++)
        {
            var item = await CourseWorkRows.InsertCourseWorkAsync(
                host, course, ct, googleId: CourseWorkTestData.ItemId(i + 1), maxPoints: maxima[i]);
            await CourseWorkRows.InsertSubmissionAsync(
                host, item, participant, ct, googleId: CourseWorkTestData.SubmissionId(i + 1),
                assignedGrade: assigned[i], draftGrade: drafts[i]);
        }

        var before = await StoredTextAsync(host, ct);

        await using (var db = Context(host.ConnectionString))
        {
            await db.Database.MigrateAsync(ct);
        }

        var after = await StoredTextAsync(host, ct);
        Assert.Equal(maxima.Length, after.Count);
        Assert.Equal(before, after);
        Assert.Contains(after, row => row.Contains("999999.9999|", StringComparison.Ordinal));
        Assert.Contains(after, row => row.Contains("|null|", StringComparison.Ordinal));
    }

    /// <summary>
    /// AC-001 … AC-003, FR-001, FR-002, VR-002: values above the old maximum and with more than four decimal places are
    /// written through EF Core, stored as those exact numbers and read back equal.
    /// </summary>
    [Theory]
    [InlineData("12345678.5", "2500000", "1000000.25")]
    [InlineData("10.123456", "7.654321", "0.000001")]
    [InlineData("99999999999999999999.123456", "1000000.0001", "123456789012.987654321")]
    public async Task LargeAndFineValues_AreStoredExactly_AndReadBackEqual(string max, string grade, string draft)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var maxPoints = decimal.Parse(max, System.Globalization.CultureInfo.InvariantCulture);
        var assignedGrade = decimal.Parse(grade, System.Globalization.CultureInfo.InvariantCulture);
        var draftGrade = decimal.Parse(draft, System.Globalization.CultureInfo.InvariantCulture);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var participant = await CourseRows.InsertParticipantAsync(host, ct);

        long itemId;
        long submissionId;
        await using (var db = Context(host.ConnectionString))
        {
            var item = Domain.Entities.CourseWork.Import(
                course,
                CourseWorkTestData.ItemId(1),
                Domain.Enums.CourseWorkResource.CourseWork,
                new Domain.Entities.CourseWorkDetails(
                    CourseWorkTestData.Title(1), host.Time.GetUtcNow(), null, maxPoints, null, null));
            db.CourseWorks.Add(item);
            await db.SaveChangesAsync(ct);
            itemId = item.Id;

            var submission = Domain.Entities.Submission.Import(
                itemId,
                participant,
                CourseWorkTestData.SubmissionId(1),
                SubmissionDetailsWith(assignedGrade, draftGrade));
            db.Submissions.Add(submission);
            await db.SaveChangesAsync(ct);
            submissionId = submission.Id;
        }

        Assert.True(await host.ScalarAsync<bool>(
            "SELECT max_points = @value FROM course_work WHERE id = @id", ct, ("value", maxPoints), ("id", itemId)));
        Assert.True(await host.ScalarAsync<bool>(
            "SELECT assigned_grade = @grade AND draft_grade = @draft FROM submission WHERE id = @id",
            ct,
            ("grade", assignedGrade),
            ("draft", draftGrade),
            ("id", submissionId)));

        await using (var db = Context(host.ConnectionString))
        {
            var loadedItem = await db.CourseWorks.AsNoTracking().SingleAsync(c => c.Id == itemId, ct);
            var loadedSubmission = await db.Submissions.AsNoTracking().SingleAsync(s => s.Id == submissionId, ct);
            Assert.Equal(maxPoints, loadedItem.MaxPoints);
            Assert.Equal(assignedGrade, loadedSubmission.AssignedGrade);
            Assert.Equal(draftGrade, loadedSubmission.DraftGrade);
        }
    }

    /// <summary>
    /// Db-design §3.1, §7: the CHECK constraints stay by name and text — a negative maximum or grade is still refused by
    /// the database. Green before the change by design: the regression guard the design asks for.
    /// </summary>
    [Fact]
    public async Task NegativeValues_AreStillRefusedByTheExistingChecks()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var participant = await CourseRows.InsertParticipantAsync(host, ct);
        var item = await CourseWorkRows.InsertCourseWorkAsync(host, course, ct, maxPoints: 10m);

        var max = await Assert.ThrowsAsync<PostgresException>(() => CourseWorkRows.InsertCourseWorkAsync(
            host, course, ct, googleId: CourseWorkTestData.ItemId(2), maxPoints: -1m));
        Assert.Equal("23514", max.SqlState);
        Assert.Equal("ck_course_work_max_points_non_negative", max.ConstraintName);

        foreach (var (assigned, draft, ordinal) in new (decimal?, decimal?, int)[] { (-0.5m, null, 2), (null, -1m, 3) })
        {
            var grade = await Assert.ThrowsAsync<PostgresException>(() => CourseWorkRows.InsertSubmissionAsync(
                host, item, participant, ct, googleId: CourseWorkTestData.SubmissionId(ordinal),
                assignedGrade: assigned, draftGrade: draft));
            Assert.Equal("23514", grade.SqlState);
            Assert.Equal("ck_submission_grades_non_negative", grade.ConstraintName);
        }
    }

    private static Domain.Entities.SubmissionDetails SubmissionDetailsWith(decimal assigned, decimal draft) =>
        new(
            Domain.Enums.SubmissionState.TurnedIn,
            null,
            assigned,
            draft,
            null,
            false,
            null);

    /// <summary>Every row's three values as PostgreSQL prints them, ordered by Google id; <c>null</c> spelled out.</summary>
    private static Task<IReadOnlyList<string>> StoredTextAsync(InstallationTestHost host, CancellationToken ct) =>
        host.QueryAsync(
            """
            SELECT coalesce(w.max_points::text, 'null') || '|' || coalesce(s.assigned_grade::text, 'null') || '|'
                   || coalesce(s.draft_grade::text, 'null')
            FROM course_work w JOIN submission s ON s.course_work_id = w.id
            ORDER BY w.google_id
            """,
            r => r.GetString(0),
            ct);
}
