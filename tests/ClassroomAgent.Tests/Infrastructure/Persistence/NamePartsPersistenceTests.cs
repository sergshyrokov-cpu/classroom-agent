using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-042 db-design §2 … §5 (AC-001, AC-005, AC-012) against real PostgreSQL (TC-2): the migration
/// <c>ParticipantNamePartsAndTemplateNameSource</c>, the <c>name_source</c> column and its check, the existing templates
/// it fills with <c>profile</c>, and the round trips of the name parts and the name source through EF Core.
/// </summary>
public sealed class NamePartsPersistenceTests(PostgreSqlFixture database)
{
    /// <summary>The last US-027 migration — the schema every existing installation has before this Story.</summary>
    private const string BeforeThisStory = "20261004202609_ReportTemplates";

    /// <summary>The production options (snake_case naming, PC-5), as the other persistence tests build them.</summary>
    private static ClassroomAgentDbContext Context(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<ClassroomAgentDbContext>();
        ClassroomAgentDbContextOptions.Configure(builder, connectionString);
        builder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)); // as InstallationTestHost migrates
        return new ClassroomAgentDbContext(builder.Options);
    }

    /// <summary>Db-design §4: the Story ships exactly one migration under the designed name, after US-027's.</summary>
    [Fact]
    public async Task TheMigration_ExistsUnderTheDesignedName()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var db = Context(host.ConnectionString);

        var migrations = db.Database.GetMigrations().ToList();

        var ours = Assert.Single(migrations, m => m.EndsWith("_ParticipantNamePartsAndTemplateNameSource", StringComparison.Ordinal));
        Assert.True(string.CompareOrdinal(ours, BeforeThisStory) > 0);
        Assert.Equal(ours, migrations[^1]);
    }

    /// <summary>
    /// Db-design §3, D-4, D-5: <c>name_source</c> is <c>varchar(8) NOT NULL</c> with no lasting default and a check
    /// that admits exactly <c>profile</c> and <c>email</c>.
    /// </summary>
    [Fact]
    public async Task NameSource_IsANonNullCodeWithACheckAndNoDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var column = await host.QueryAsync(
            """
            SELECT data_type || '|' || character_maximum_length || '|' || is_nullable || '|' || coalesce(column_default, '')
            FROM information_schema.columns
            WHERE table_schema = current_schema() AND table_name = 'report_template' AND column_name = 'name_source'
            """,
            r => r.GetString(0),
            ct);
        Assert.Equal(["character varying|8|NO|"], column);

        foreach (var value in new[] { "profile", "email" })
        {
            await InsertTemplateAsync(host, "Test " + value, value, ct);
        }

        foreach (var value in new[] { "full", "Email", "" })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => InsertTemplateAsync(host, "Bad " + value, value, ct));
            Assert.Equal("23514", error.SqlState);
            Assert.Equal("ck_report_template_name_source", error.ConstraintName);
        }

        var missing = await Assert.ThrowsAsync<PostgresException>(() => host.ExecuteAsync(
            """
            INSERT INTO report_template (name, normalized_name, view, hide_materials, hours_per_lesson, scale_mode,
                                         late_mark_kind, late_mark_text, author_id, created_at, updated_at)
            VALUES ('No Source', 'NO SOURCE', 'full', false, 2, 'none', 'program', NULL, 1, now(), now())
            """,
            ct));
        Assert.Equal("23502", missing.SqlState);
    }

    /// <summary>
    /// AC-005, db-design §4: a template that existed before this Story reads <c>profile</c> after the migration; existing
    /// participants keep no surname or given name (no backfill from the full name, spec FR-001).
    /// </summary>
    [Fact]
    public async Task TheMigration_GivesExistingTemplatesTheProfile_AndInventsNoNameParts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct, migrate: false);
        await using (var db = Context(host.ConnectionString))
        {
            await db.GetService<IMigrator>().MigrateAsync(BeforeThisStory, ct);
        }

        var stamp = host.Time.GetUtcNow();
        var template = await host.ScalarAsync<long>(
            """
            INSERT INTO report_template (name, normalized_name, view, hide_materials, hours_per_lesson, scale_mode,
                                         late_mark_kind, late_mark_text, author_id, created_at, updated_at)
            VALUES ('Old Template', 'OLD TEMPLATE', 'full', false, 2, 'none', 'program', NULL, 1, @stamp, @stamp)
            RETURNING id
            """,
            ct,
            ("stamp", stamp));
        var participant = await CourseRows.InsertParticipantAsync(
            host, ct, googleUserId: CourseTestData.UserId(1), email: CourseTestData.Email("person1"), fullName: "Olena Testova");

        await using (var db = Context(host.ConnectionString))
        {
            await db.Database.MigrateAsync(ct);
        }

        Assert.Equal("profile", await host.NameSourceOfAsync(template, ct));
        var parts = await host.QueryAsync(
            "SELECT surname IS NULL AND given_name IS NULL, full_name FROM classroom_participant WHERE id = @id",
            r => (Empty: r.GetBoolean(0), FullName: r.GetString(1)),
            ct,
            ("id", participant));
        Assert.Equal([(true, "Olena Testova")], parts);
    }

    /// <summary>Db-design D-6: no index is added on the new columns.</summary>
    [Fact]
    public async Task NoIndex_CoversTheNewColumns()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var indexes = await host.QueryAsync(
            """
            SELECT indexdef FROM pg_indexes
            WHERE schemaname = current_schema() AND tablename IN ('classroom_participant', 'report_template')
            """,
            r => r.GetString(0),
            ct);

        // The columns must exist for the absence below to mean anything (a vacuous pass before the migration otherwise).
        Assert.True(await host.ColumnExistsAsync("classroom_participant", "surname", ct));
        Assert.True(await host.ColumnExistsAsync("classroom_participant", "given_name", ct));
        Assert.True(await host.ColumnExistsAsync("report_template", "name_source", ct));
        Assert.Contains(indexes, d => d.Contains("google_user_id", StringComparison.Ordinal));
        Assert.DoesNotContain(indexes, d => d.Contains("surname", StringComparison.Ordinal)
            || d.Contains("given_name", StringComparison.Ordinal)
            || d.Contains("name_source", StringComparison.Ordinal));
    }

    /// <summary>AC-001, db-design §2: the entity's name parts are written to and read from their columns.</summary>
    [Fact]
    public async Task AParticipantsNameParts_RoundTrip()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        long id;
        await using (var db = Context(host.ConnectionString))
        {
            var participant = ClassroomParticipant.Import(
                CourseTestData.UserId(1), CourseTestData.Email("person1"), "Olena Testova", "  Тестова ", "Олена");
            db.ClassroomParticipants.Add(participant);
            await db.SaveChangesAsync(ct);
            id = participant.Id;
        }

        var stored = await host.QueryAsync(
            "SELECT surname, given_name FROM classroom_participant WHERE id = @id",
            r => (r.GetString(0), r.GetString(1)),
            ct,
            ("id", id));
        Assert.Equal([("Тестова", "Олена")], stored);

        await using (var db = Context(host.ConnectionString))
        {
            var loaded = await db.ClassroomParticipants.SingleAsync(p => p.Id == id, ct);
            Assert.Equal(("Тестова", "Олена"), (loaded.Surname, loaded.GivenName));
            loaded.UpdateFrom(CourseTestData.Email("person1"), "Olena Testova", null, "Олена");
            await db.SaveChangesAsync(ct);
        }

        var cleared = await host.QueryAsync(
            "SELECT surname IS NULL, given_name FROM classroom_participant WHERE id = @id",
            r => (r.GetBoolean(0), r.GetString(1)),
            ct,
            ("id", id));
        Assert.Equal([(true, "Олена")], cleared);
    }

    /// <summary>AC-005, db-design §3: the repository stores the template's name source as its code and reads it back.</summary>
    [Fact]
    public async Task ATemplatesNameSource_RoundTripsThroughTheRepository()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        long id = 0;
        await host.WithTemplateRepositoryAsync(async (repo, work) =>
        {
            var template = ReportTemplate.Create(
                "Test Template Email", ReportTemplateTestData.Settings(nameSource: ReportNameSource.Email), 1);
            repo.Add(template);
            await work.SaveChangesAsync(ct);
            id = template.Id;
        });

        Assert.Equal("email", await host.NameSourceOfAsync(id, ct));
        await host.WithTemplateRepositoryAsync(async (repo, work) =>
        {
            var loaded = await repo.GetAsync(id, forUpdate: true, ct);
            Assert.NotNull(loaded);
            Assert.Equal(ReportNameSource.Email, loaded.NameSource);
            loaded.Change(loaded.Name, ReportTemplateTestData.Settings(nameSource: ReportNameSource.Profile));
            repo.MarkChanged(loaded);
            await work.SaveChangesAsync(ct);
        });

        Assert.Equal("profile", await host.NameSourceOfAsync(id, ct));
    }

    private static Task<int> InsertTemplateAsync(InstallationTestHost host, string name, string nameSource, CancellationToken ct) =>
        host.ExecuteAsync(
            """
            INSERT INTO report_template (name, normalized_name, view, hide_materials, hours_per_lesson, scale_mode,
                                         late_mark_kind, late_mark_text, author_id, created_at, updated_at, name_source)
            VALUES (@name, upper(@name), 'full', false, 2, 'none', 'program', NULL, 1, now(), now(), @source)
            """,
            ct,
            ("name", name),
            ("source", nameSource));
}
