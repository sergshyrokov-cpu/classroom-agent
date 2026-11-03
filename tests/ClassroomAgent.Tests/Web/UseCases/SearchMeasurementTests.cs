using System.Diagnostics;
using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-071 AC-010, spec section 9: the data and the three texts of the measurement, on real PostgreSQL with no new index. It
/// asserts what was found, never a time ("a recorded measurement, not a pass-or-fail timing in the suite"): the median and the
/// worst of five runs after one warm-up go to the test output and to <c>US-071-search-measure.txt</c> in the temporary folder, for
/// the implementation report. Synthetic data only (TC-4).
/// </summary>
public sealed class SearchMeasurementTests(PostgreSqlFixture database)
{
    private const int Students = 1000;

    private const int Teachers = 300;

    private const int Courses = 600;

    private const int MembershipsPerStudent = 10;

    private const int Codes = 300;

    /// <summary>Spec section 9: the proportions, by one set of SQL statements, so a run takes seconds.</summary>
    private static async Task SeedAsync(InstallationTestHost host, CancellationToken ct)
    {
        await host.ExecuteAsync(
            $"""
            INSERT INTO course (google_id, name, section, course_state, created_at, updated_at)
            SELECT 'gc' || n, 'Предмет ' || n, '10-' || (n % 9), 'active', now(), now() FROM generate_series(1, {Courses}) n
            """,
            ct);
        await host.ExecuteAsync(
            $"""
            INSERT INTO classroom_participant (google_user_id, email, surname, given_name, created_at, updated_at)
            SELECT 'gu' || n, 'student' || n || '@school-one.example.test', 'Прізвище' || n, 'Анна', now(), now()
            FROM generate_series(1, {Students}) n
            """,
            ct);
        await host.ExecuteAsync(
            $"""
            INSERT INTO classroom_participant (google_user_id, email, surname, given_name, created_at, updated_at)
            SELECT 'gt' || n, 'teacher' || n || '@school-one.example.test', 'Викладач' || n, 'Олена', now(), now()
            FROM generate_series(1, {Teachers}) n
            """,
            ct);
        await host.ExecuteAsync(
            $"""
            INSERT INTO course_membership (course_id, participant_id, role, first_seen_at, last_seen_at, on_roster, created_at, updated_at)
            SELECT c.id, p.id, 'student', now() - interval '9 days', now(), (p.id + c.id) % 7 <> 0, now(), now()
            FROM classroom_participant p
            CROSS JOIN generate_series(0, {MembershipsPerStudent - 1}) k
            JOIN course c ON c.google_id = 'gc' || (((p.id + k * 57) % {Courses}) + 1)
            WHERE p.google_user_id LIKE 'gu%'
            ON CONFLICT DO NOTHING
            """,
            ct);
        await host.ExecuteAsync(
            $"""
            INSERT INTO course_membership (course_id, participant_id, role, first_seen_at, last_seen_at, on_roster, created_at, updated_at)
            SELECT c.id, p.id, 'teacher', now(), now(), true, now(), now()
            FROM classroom_participant p
            JOIN course c ON c.google_id = 'gc' || ((p.id % {Courses}) + 1)
            WHERE p.google_user_id LIKE 'gt%'
            """,
            ct);
        await host.ExecuteAsync(
            $"""
            INSERT INTO meet_session (conference_id, meeting_code, organizer_email, started_at, ended_at, created_at, updated_at)
            SELECT 'conf-' || n, 'zzz-' || lpad(n::text, 4, '0') || '-aaa', 'teacher' || (n % {Teachers}) || '@school-one.example.test',
                   now(), now(), now(), now()
            FROM generate_series(1, {Codes}) n
            """,
            ct);
    }

    /// <summary>AC-010: the three texts on the expected size; the groups hold what the data says, and the times are recorded.</summary>
    [Fact]
    public async Task TheSearch_OnTheExpectedSize_IsMeasured()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        await SeedAsync(host, ct);

        using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>();
        var query = new GetSearchResultsQuery(
            new StudentSearchSource(db),
            new TeacherListSource(db),
            new CourseListSource(db),
            new MeetCodesReadSource(db),
            new SchoolTimeZone(JournalTestData.Kyiv));

        var lines = new List<string>
        {
            $"students={Students} teachers={Teachers} courses={Courses} memberships/student={MembershipsPerStudent} meet codes={Codes} (no submissions: search reads none)",
        };

        // A common 2-character word matching many, a 2-word name, a text matching nothing (spec section 9).
        var common = await RunAsync(query, "ан", lines, ct);
        var name = await RunAsync(query, "прізвище123 анна", lines, ct);
        var nothing = await RunAsync(query, "ъъъъъъ", lines, ct);

        Assert.Equal(Students, common.Students.Total);
        Assert.Equal(20, common.Students.Rows.Count);
        Assert.All(common.Students.Rows, row => Assert.NotEmpty(row.Courses));
        Assert.Equal(1, name.Students.Total);
        Assert.Equal(0, nothing.Students.Total + nothing.Teachers.Total + nothing.Courses.Total + nothing.MeetCodes.Total);

        var report = string.Join(Environment.NewLine, lines);
        TestContext.Current.TestOutputHelper?.WriteLine(report);
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(), "US-071-search-measure.txt"), report, ct);
    }

    private static async Task<SearchResultsPageModel> RunAsync(GetSearchResultsQuery query, string text, List<string> lines, CancellationToken ct)
    {
        var request = new SearchResultsRequest([text]);
        var page = await query.ExecuteAsync(request, ct);
        var times = new List<double>();
        for (var i = 0; i < 5; i++)
        {
            var watch = Stopwatch.StartNew();
            page = await query.ExecuteAsync(request, ct);
            times.Add(watch.Elapsed.TotalMilliseconds);
        }

        times.Sort();
        lines.Add(string.Create(CultureInfo.InvariantCulture, $"'{text}': median {times[2]:F0} ms, worst {times[4]:F0} ms"));
        return page;
    }
}
