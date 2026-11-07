using System.Diagnostics;
using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-074 AC-008, spec FR-005, db-design §6: search by organizer name on the expected size (about 1000 students and 300 teachers,
/// every participant named), on real PostgreSQL with no new index. As in US-071 it asserts what was found, never a time: the median
/// and the worst of five runs after one warm-up go to the test output and to <c>US-074-name-search-measure.txt</c> in the temporary
/// folder, for the implementation report. Synthetic data only (TC-4).
/// </summary>
public sealed class MeetCodeNameSearchMeasurementTests(PostgreSqlFixture database)
{
    private const int Students = 1000;

    private const int Teachers = 300;

    private const int Codes = 300;

    private static readonly TimeZoneInfo Zone = JournalTestData.Kyiv;

    /// <summary>Named participants and one code per teacher, organized by that teacher, by one set of SQL statements.</summary>
    private static async Task SeedAsync(InstallationTestHost host, CancellationToken ct)
    {
        await host.ExecuteAsync(
            $"""
            INSERT INTO classroom_participant (google_user_id, email, full_name, surname, given_name, created_at, updated_at)
            SELECT 'gu' || n, 'student' || n || '@school-one.example.test', 'Анна Прізвище' || n, 'Прізвище' || n, 'Анна', now(), now()
            FROM generate_series(1, {Students}) n
            """,
            ct);
        await host.ExecuteAsync(
            $"""
            INSERT INTO classroom_participant (google_user_id, email, full_name, surname, given_name, created_at, updated_at)
            SELECT 'gt' || n, 'teacher' || n || '@school-one.example.test', 'Олена Викладачка' || n, 'Викладачка' || n, 'Олена', now(), now()
            FROM generate_series(1, {Teachers}) n
            """,
            ct);
        await host.ExecuteAsync(
            $"""
            INSERT INTO meet_session (conference_id, meeting_code, organizer_email, started_at, ended_at, created_at, updated_at)
            SELECT 'conf-' || n, 'zzz-' || lpad(n::text, 4, '0') || '-aaa', 'teacher' || n || '@school-one.example.test',
                   now(), now(), now(), now()
            FROM generate_series(1, {Codes}) n
            """,
            ct);
    }

    /// <summary>AC-008: a name of one teacher, a word common to every teacher, and a text matching nothing; found counts and times.</summary>
    [Fact]
    public async Task SearchByOrganizerName_OnTheExpectedSize_IsMeasured()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        await SeedAsync(host, ct);

        using var scope = host.CreateScope();
        var source = new MeetCodesReadSource(scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>());
        var lines = new List<string>
        {
            $"students={Students} teachers={Teachers} meet codes={Codes}, every participant named, no new index",
        };

        var one = await RunAsync(source, "викладачка123 олена", lines, ct);
        var common = await RunAsync(source, "викладачка", lines, ct);
        var nothing = await RunAsync(source, "ъъъъъъ", lines, ct);

        Assert.Equal(new MeetCodeListCounts(1, 0, 0), one.Counts);
        Assert.Equal(["zzz-0123-aaa"], one.Page.Rows.Select(r => r.MeetingCode));
        Assert.Equal("Олена Викладачка123", one.Page.Rows.Single().Organizers.Single().StoredFullName);
        Assert.Equal(new MeetCodeListCounts(Codes, 0, 0), common.Counts);
        Assert.Equal(20, common.Page.Rows.Count);
        Assert.Equal(new MeetCodeListCounts(0, 0, 0), nothing.Counts);

        var report = string.Join(Environment.NewLine, lines);
        TestContext.Current.TestOutputHelper?.WriteLine(report);
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(), "US-074-name-search-measure.txt"), report, ct);
    }

    private static async Task<(MeetCodeListCounts Counts, PagedRows<UnassignedCodeRow> Page)> RunAsync(
        MeetCodesReadSource source, string text, List<string> lines, CancellationToken ct)
    {
        var counts = await source.GetCountsAsync(text, ct);
        var page = await source.GetUnassignedPageAsync(0, 20, Zone, text, ct);
        var times = new List<double>();
        for (var i = 0; i < 5; i++)
        {
            var watch = Stopwatch.StartNew();
            counts = await source.GetCountsAsync(text, ct);
            page = await source.GetUnassignedPageAsync(0, 20, Zone, text, ct);
            times.Add(watch.Elapsed.TotalMilliseconds);
        }

        times.Sort();
        lines.Add(string.Create(CultureInfo.InvariantCulture, $"'{text}': counts + page median {times[2]:F0} ms, worst {times[4]:F0} ms"));
        return (counts, page);
    }
}
