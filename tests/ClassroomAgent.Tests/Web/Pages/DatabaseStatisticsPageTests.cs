using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Data = ClassroomAgent.Tests.TestInfrastructure.DatabaseStatisticsTestData;
using Expected = ClassroomAgent.Tests.TestInfrastructure.DatabaseStatisticsHostExtensions.Expected;
using Mr = ClassroomAgent.Tests.TestInfrastructure.MeetReportsHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-024 over HTTP: the home page section, the twelve counters, the synchronization block (never run, completed,
/// failed, running), the failure guidance by role, the school's time zone and culture format, the ignored query string,
/// the form and the absence of any personal data (AC-001 … AC-007, AC-013, AC-014, AC-019). Rows are seeded by SQL; the
/// markup hooks are <see cref="DatabaseStatisticsTestData.Markup"/>.
/// </summary>
public sealed class DatabaseStatisticsPageTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset LastSuccessfulRunAt = new(2026, 3, 1, 22, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset LastFullRunAt = new(2026, 3, 1, 22, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset MeetLoadedUpTo = new(2026, 3, 1, 22, 15, 0, TimeSpan.Zero);

    public static TheoryData<Actor> BothRoles => new(Actor.Admin, Actor.Dean);

    public static TheoryData<Actor, ReadOnlyModeHost.Cause> RolesAndCauses => new()
    {
        { Actor.Admin, ReadOnlyModeHost.Cause.NotReadOnly },
        { Actor.Dean, ReadOnlyModeHost.Cause.NotReadOnly },
        { Actor.Admin, ReadOnlyModeHost.Cause.Suspended },
        { Actor.Dean, ReadOnlyModeHost.Cause.Suspended },
    };

    public static TheoryData<Actor, string> RolesAndStatuses => new()
    {
        { Actor.Admin, "running" },
        { Actor.Dean, "running" },
        { Actor.Admin, "completed" },
        { Actor.Dean, "completed" },
    };

    public static TheoryData<ReadOnlyModeHost.Cause> AllCauses => new(
        ReadOnlyModeHost.Cause.NotReadOnly,
        ReadOnlyModeHost.Cause.NeverConfirmed,
        ReadOnlyModeHost.Cause.Suspended,
        ReadOnlyModeHost.Cause.GracePeriodExpired);

    private static Dictionary<string, int> ExpectedCounters => new(StringComparer.Ordinal)
    {
        [Data.Counters.Active] = Expected.Active,
        [Data.Counters.Archived] = Expected.Archived,
        [Data.Counters.Provisioned] = Expected.Provisioned,
        [Data.Counters.Declined] = Expected.Declined,
        [Data.Counters.Suspended] = Expected.Suspended,
        [Data.Counters.Total] = Expected.Total,
        [Data.Counters.Teachers] = Expected.Teachers,
        [Data.Counters.Students] = Expected.Students,
        [Data.Counters.CourseWorkItems] = Expected.CourseWorkItems,
        [Data.Counters.Submissions] = Expected.Submissions,
        [Data.Counters.MeetMeetings] = Expected.MeetMeetings,
        [Data.Counters.MeetCodesAwaitingDecision] = Expected.MeetCodesAwaitingDecision,
    };

    private static string? OpeningTag(string html, string tag, string id)
    {
        var match = Regex.Match(
            html, @"<" + tag + @"\b[^>]*\sid=""" + Regex.Escape(id) + @"""[^>]*>", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : null;
    }

    private static string? Attribute(string tag, string name)
    {
        var match = Regex.Match(tag, @"\s" + name + @"\s*=\s*(""(?<v>[^""]*)""|'(?<v>[^']*)')", RegexOptions.IgnoreCase);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["v"].Value) : null;
    }

    private static void AssertCounters(string body, IReadOnlyDictionary<string, int> expected)
    {
        foreach (var (name, value) in expected)
        {
            var shown = Data.Counter(body, name);
            Assert.True(shown == value, $"Counter '{name}' shows {shown?.ToString() ?? "nothing"}, expected {value}.");
        }
    }

    private static List<int?> ShownCounters(string body) => Data.Counters.All.Select(c => Data.Counter(body, c)).ToList();

    /// <summary>AC-001: the home page has the "Database" section with exactly one link, after the report templates and before the Admin's settings.</summary>
    [Theory]
    [MemberData(nameof(RolesAndCauses))]
    public async Task TheHomePage_HasTheDatabaseSection_AfterTheReportTemplates(Actor actor, ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct, cause);
        await using var _host = host;

        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.OK, landing.Status);
        var section = Regex.Match(
            landing.Body,
            @"<nav\b[^>]*\sid=""" + Data.Markup.Section + @"""[^>]*>(?<inner>.*?)</nav>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(section.Success, "The home page has no <nav id=\"database-section\">.");
        var links = Regex.Matches(section.Groups["inner"].Value, @"<a\b[^>]*>", RegexOptions.IgnoreCase);
        var link = Assert.Single(links);
        Assert.Equal(Data.Path, Attribute(link.Value, "href"));
        var templates = landing.Body.IndexOf("id=\"report-templates-section\"", StringComparison.Ordinal);
        Assert.True(templates >= 0, "The home page has no report templates section.");
        Assert.True(section.Index > templates, "The Database section is not after the report templates section.");
        if (actor == Actor.Admin)
        {
            var settings = landing.Body.IndexOf("id=\"admin-settings\"", StringComparison.Ordinal);
            Assert.True(settings >= 0, "The Admin's home page has no settings section.");
            Assert.True(section.Index < settings, "The Database section is not before the Admin's settings.");
        }
        else
        {
            Assert.Equal(-1, landing.Body.IndexOf("<nav", section.Index + section.Length, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>AC-002, AC-003, AC-004, AC-015, AC-018: every counter of the seeded school, for both roles.</summary>
    [Theory]
    [MemberData(nameof(BothRoles))]
    public async Task TheSeededSchool_ShowsEveryCounter(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        await host.SeedStatisticsWorldAsync(ct);

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        AssertCounters(page.Body, ExpectedCounters);
    }

    /// <summary>AC-006: an empty school that never synchronized shows twelve zeros and "not yet" on all four lines.</summary>
    [Fact]
    public async Task AnEmptySchool_NeverSynchronized_ShowsZerosAndNotYet()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(12, Data.Counters.All.Count);
        Assert.All(ShownCounters(page.Body), shown => Assert.Equal((int?)0, shown));
        var notYet = host.Text(Data.Keys.NotYet, "uk");
        foreach (var line in Data.SyncLines.All)
        {
            Assert.Equal(notYet, Data.SyncLine(page.Body, line));
        }
    }

    /// <summary>AC-005, AC-014: a completed run — the three times in the school's zone and the culture's format, the state, no processed count.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task ACompletedRun_ShowsTheTimesInTheSchoolZone_AndTheState_AndNoProcessedCount(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await Mr.StartAsync(database, Actor.Admin, language, ct);
        await using var _host = host;
        await host.SeedSyncStateAsync(
            "completed", ct, LastSuccessfulRunAt, LastFullRunAt, MeetLoadedUpTo, processedCount: 987654);

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(
            Data.Format(new DateTime(2026, 3, 2, 0, 30, 0), language),
            Data.SyncLine(page.Body, Data.SyncLines.LastSuccessfulRun));
        Assert.Equal(
            Data.Format(new DateTime(2026, 3, 2, 0, 0, 0), language),
            Data.SyncLine(page.Body, Data.SyncLines.LastFullRun));
        Assert.Equal(
            Data.Format(new DateTime(2026, 3, 2, 0, 15, 0), language),
            Data.SyncLine(page.Body, Data.SyncLines.MeetLoadedUpTo));
        Assert.Equal(host.Text(Data.Keys.Completed, language), Data.SyncLine(page.Body, Data.SyncLines.State));
        Assert.DoesNotContain("987654", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-005: the synchronization block reads the same to the Admin and to the Dean.</summary>
    [Fact]
    public async Task TheSynchronizationBlock_IsTheSame_ForAdminAndDean()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        var dean = await JournalHostExtensions.SignInAsync(host, Actor.Dean, ct);
        await host.SeedSyncStateAsync(
            "completed", ct, LastSuccessfulRunAt, LastFullRunAt, MeetLoadedUpTo, processedCount: 987654);

        var adminPage = await admin.GetAsync(Data.Path, ct);
        var deanPage = await dean.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, adminPage.Status);
        Assert.Equal(HttpStatusCode.OK, deanPage.Status);
        foreach (var line in Data.SyncLines.All)
        {
            var adminValue = Data.SyncLine(adminPage.Body, line);
            Assert.NotNull(adminValue);
            Assert.Equal(adminValue, Data.SyncLine(deanPage.Body, line));
        }
    }

    /// <summary>AC-007, AC-013: a failed run — "failed", the Dean's hint or the Admin's link, and neither the diagnosis nor the failed step.</summary>
    [Theory]
    [MemberData(nameof(BothRoles))]
    public async Task AFailedRun_ShowsFailed_AndTheGuidanceOfTheRole_AndNoCause(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        await host.SeedSyncStateAsync(
            "failed", ct, lastSuccessfulRunAt: LastSuccessfulRunAt, failedStep: "meet");

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(host.Text(Data.Keys.Failed, "uk"), Data.SyncLine(page.Body, Data.SyncLines.State));
        if (actor == Actor.Dean)
        {
            Assert.Equal(host.Text(Data.Keys.AskAdmin, "uk"), Html.ElementText(page.Body, Data.Markup.FailureHint));
            Assert.DoesNotContain("id=\"" + Data.Markup.ConnectionLink + "\"", page.Body, StringComparison.Ordinal);
        }
        else
        {
            var link = OpeningTag(page.Body, "a", Data.Markup.ConnectionLink);
            Assert.NotNull(link);
            Assert.Equal("/settings/workspace-connection", Attribute(link, "href"));
            Assert.DoesNotContain("id=\"" + Data.Markup.FailureHint + "\"", page.Body, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(DatabaseStatisticsHostExtensions.StoredDiagnosis, page.Text, StringComparison.Ordinal);
        var texts = host.AllTexts("uk");
        foreach (var name in Enum.GetNames<SyncDiagnosis>())
        {
            if (texts.TryGetValue("LastSync.Diagnosis." + name, out var diagnosis) && !string.IsNullOrWhiteSpace(diagnosis))
            {
                Assert.DoesNotContain(diagnosis, page.Text, StringComparison.Ordinal);
            }
        }

        foreach (var step in new[] { "Classroom", "Meet", "Linking" })
        {
            Assert.DoesNotContain(host.Text("LastSync.Step." + step, "uk"), page.Text, StringComparison.Ordinal);
        }
    }

    /// <summary>AC-007: a running run and a completed run show neither the Dean's hint nor the Admin's link.</summary>
    [Theory]
    [MemberData(nameof(RolesAndStatuses))]
    public async Task ARunningOrCompletedRun_ShowsNoFailureGuidance(Actor actor, string status)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        await host.SeedSyncStateAsync(status, ct, lastSuccessfulRunAt: LastSuccessfulRunAt);

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.DoesNotContain("id=\"" + Data.Markup.FailureHint + "\"", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"" + Data.Markup.ConnectionLink + "\"", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-019: any query parameter is ignored and echoed nowhere.</summary>
    [Fact]
    public async Task AnyQueryString_IsIgnored_AndEchoedNowhere()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedStatisticsWorldAsync(ct);

        var plain = await client.GetAsync(Data.Path, ct);
        var page = await client.GetAsync(
            Data.Path + "?x=zz-marker-zz&status=archived&returnUrl=https%3A%2F%2Fevil.example", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.DoesNotContain("zz-marker-zz", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("evil.example", page.Body, StringComparison.Ordinal);
        Assert.Equal(ShownCounters(plain.Body), ShownCounters(page.Body));
        AssertCounters(page.Body, ExpectedCounters);
    }

    /// <summary>AC-008, AC-009: the page carries the form posting a synchronization request, with the antiforgery token, in every mode.</summary>
    [Theory]
    [MemberData(nameof(AllCauses))]
    public async Task ThePage_CarriesTheSynchronizationForm_InEveryMode(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct, cause);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var form = Regex.Match(
            page.Body,
            @"(?<tag><form\b[^>]*\sid=""" + Data.Markup.Form + @"""[^>]*>)(?<inner>.*?)</form>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(form.Success, "The page has no synchronization form.");
        Assert.Equal(Data.PressPath, Attribute(form.Groups["tag"].Value, "action"));
        Assert.Equal("post", Attribute(form.Groups["tag"].Value, "method"), ignoreCase: true);
        Assert.True(Html.HasInput(form.Groups["inner"].Value, Html.AntiforgeryFieldName), "The form must carry the antiforgery token.");
    }

    /// <summary>AC-003, AC-013, S-08: no course name, person name, email or meeting code reaches the page.</summary>
    [Fact]
    public async Task TheSeededSchool_ShowsNoPersonalData()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.SeedStatisticsWorldAsync(ct);

        var page = await client.GetAsync(Data.Path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        foreach (var value in DatabaseStatisticsHostExtensions.SensitiveValues)
        {
            Assert.DoesNotContain(value, page.Text, StringComparison.Ordinal);
        }
    }
}
