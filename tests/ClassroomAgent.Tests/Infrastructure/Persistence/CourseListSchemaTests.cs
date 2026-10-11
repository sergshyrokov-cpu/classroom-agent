using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-020 db-design §2, §4.1, §5 (PC-2, PC-7) against real PostgreSQL (TC-2): the migration <c>AddCourseListSupport</c>
/// creates the deterministic ICU collation <c>course_name_uk</c> and the partial index of current teachers, and changes
/// no column of the three US-014 tables.
/// </summary>
public sealed class CourseListSchemaTests(PostgreSqlFixture database)
{
    private static ClassroomAgentDbContext Context(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<ClassroomAgentDbContext>();
        ClassroomAgentDbContextOptions.Configure(builder, connectionString);
        builder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)); // as InstallationTestHost migrates
        return new ClassroomAgentDbContext(builder.Options);
    }

    /// <summary>db-design §5: the Story ships one migration under the designed name.</summary>
    [Fact]
    public async Task TheMigration_ExistsUnderTheDesignedName()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var db = Context(host.ConnectionString);

        var migrations = db.Database.GetMigrations().ToList();

        Assert.Single(migrations, m => m.EndsWith("_AddCourseListSupport", StringComparison.Ordinal));
    }

    /// <summary>db-design §2.1: the collation is ICU, locale uk-UA, deterministic.</summary>
    [Fact]
    public async Task TheCollation_IsADeterministicUkrainianIcuCollation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var rows = await host.QueryAsync(
            """
            SELECT collprovider::text, collisdeterministic, colllocale
            FROM pg_collation
            WHERE collname = 'course_name_uk' AND collnamespace = 'public'::regnamespace
            """,
            r => (Provider: r.GetString(0), Deterministic: r.GetBoolean(1), Locale: r.IsDBNull(2) ? null : r.GetString(2)),
            ct);

        var collation = Assert.Single(rows);
        Assert.Equal("i", collation.Provider);
        Assert.True(collation.Deterministic);
        Assert.Equal("uk-UA", collation.Locale);
    }

    /// <summary>db-design §2.1: the collation is applied to no column — name and section keep the default.</summary>
    [Fact]
    public async Task NoColumn_UsesTheCollation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await host.QueryAsync(
            """
            SELECT table_name || '.' || column_name
            FROM information_schema.columns
            WHERE table_schema = 'public' AND collation_name = 'course_name_uk'
            """,
            r => r.GetString(0),
            ct);

        Assert.Empty(columns);
    }

    /// <summary>db-design §2.2: under the collation Cyrillic lower-cases (ІСТОРІЯ ЇЖАК ҐАНОК ЄВА) and sorts in Ukrainian order.</summary>
    [Fact]
    public async Task TheCollation_FoldsAndOrdersUkrainian()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var lowered = await host.ScalarAsync<string>(
            "SELECT lower('ІСТОРІЯ ЇЖАК ҐАНОК ЄВА' COLLATE course_name_uk)", ct);
        var ordered = await host.ScalarAsync<string>(
            """
            SELECT string_agg(n, '|' ORDER BY n COLLATE course_name_uk)
            FROM unnest(ARRAY['Історія', 'Інформатика', 'Географія', 'Ґрунтознавство', 'Алгебра', 'Physics']) AS n
            """,
            ct);

        Assert.Equal("історія їжак ґанок єва", lowered);
        Assert.Equal("Алгебра|Географія|Ґрунтознавство|Інформатика|Історія|Physics", ordered);
    }

    /// <summary>db-design §4.1: the partial index covers (participant_id, course_id) of current teachers only.</summary>
    [Fact]
    public async Task TheCurrentTeacherIndex_IsPartialOnCurrentTeachers()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var definition = await host.ScalarAsync<string>(
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND indexname = @name",
            ct,
            ("name", CourseTestData.Constraints.MembershipCurrentTeacher));

        Assert.NotNull(definition);
        Assert.Contains("ON public.course_membership", definition, StringComparison.Ordinal);
        Assert.Contains("(participant_id, course_id)", definition, StringComparison.Ordinal);
        var predicate = definition[definition.IndexOf(" WHERE ", StringComparison.Ordinal)..];
        Assert.Contains("'teacher'", predicate, StringComparison.Ordinal);
        Assert.Contains("on_roster = true", predicate, StringComparison.Ordinal);
        Assert.DoesNotContain("UNIQUE", definition, StringComparison.Ordinal);
    }
}
