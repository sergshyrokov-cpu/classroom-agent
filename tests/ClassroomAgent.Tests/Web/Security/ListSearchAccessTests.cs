using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Ls = ClassroomAgent.Tests.TestInfrastructure.ListSearchTestData;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-070 AC-008, TC-5: the three searched lists keep their authorization (Admin and Dean allowed, an anonymous visitor
/// sent to sign-in, a Dean on the forced temporary-password session sent to the password change), search in read-only mode,
/// read stored data only (no Google call, nothing written, no audit event) and leave the searched text out of every log line.
/// </summary>
public sealed class ListSearchAccessTests(PostgreSqlFixture database)
{
    private const string ChangePasswordPath = "/sign-in/change-password";

    private const string Courses = "courses";

    private const string Teachers = "teachers";

    private const string MeetCodes = "meet-codes";

    public static TheoryData<string> Lists => new(Courses, Teachers, MeetCodes);

    public static TheoryData<Actor, string> AllowedCases
    {
        get
        {
            var data = new TheoryData<Actor, string>();
            foreach (var actor in new[] { Actor.Admin, Actor.Dean })
            {
                foreach (var list in new[] { Courses, Teachers, MeetCodes })
                {
                    data.Add(actor, list);
                }
            }

            return data;
        }
    }

    private static string UrlOf(string list, string text) => list switch
    {
        Courses => CourseListTestData.Url(q: text),
        Teachers => Ls.TeachersUrl(q: text),
        MeetCodes => Ls.MeetUrl(q: text),
        _ => throw new ArgumentOutOfRangeException(nameof(list), list, null),
    };

    private static string PathOf(string list) => list switch
    {
        Courses => CourseListTestData.Path,
        Teachers => TeacherListTestData.Path,
        MeetCodes => Mc.Path,
        _ => throw new ArgumentOutOfRangeException(nameof(list), list, null),
    };

    private static string FieldOf(string list) => list switch
    {
        Courses => Ls.CourseField,
        Teachers => Ls.TeacherField,
        MeetCodes => Ls.MeetField,
        _ => throw new ArgumentOutOfRangeException(nameof(list), list, null),
    };

    /// <summary>The text of the one row each list holds after <see cref="SeedAsync"/>.</summary>
    private static string RowTextOf(string list) => list switch
    {
        Courses => Ls.Needle + " курс",
        Teachers => Ls.Needle + "чук",
        MeetCodes => Ls.Needle + "-aaaa",
        _ => throw new ArgumentOutOfRangeException(nameof(list), list, null),
    };

    /// <summary>One row in each list, whose searchable text contains <see cref="Ls.Needle"/>.</summary>
    private static async Task SeedAsync(InstallationTestHost host, CancellationToken ct)
    {
        await host.PrepareAsync(ct);
        var course = await host.InsertCourseAsync(1, Ls.Needle + " курс", ct);
        var teacher = await host.InsertPersonAsync(1, Ls.Needle + "чук", "Олена", CourseTestData.Email("zz.access"), ct);
        await host.AddTeacherAsync(course, teacher, ct);
        await host.SeedMeetingAsync(Ls.Needle + "-aaaa", Mc.Email("org.one"), new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero), ct);
    }

    /// <summary>AC-008: an Admin and a Dean both search every list and find the row.</summary>
    [Theory]
    [MemberData(nameof(AllowedCases))]
    public async Task AnAdminAndADean_CanSearchEveryList(Actor actor, string list)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(UrlOf(list, Ls.Needle), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(Ls.Needle, Ls.FieldValue(page.Body, FieldOf(list)));
        Assert.Contains(RowTextOf(list), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-008, TC-5: an anonymous visitor is sent to sign-in, with no list and no field.</summary>
    [Theory]
    [MemberData(nameof(Lists))]
    public async Task Anonymous_IsSentToSignIn(string list)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(UrlOf(list, Ls.Needle), ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.StartsWith(SignInTestData.SignInPath, page.LocationPath, StringComparison.Ordinal);
        Assert.Null(Ls.TagOf(page.Body, FieldOf(list)));
    }

    /// <summary>AC-008, TC-5: a Dean on the forced temporary-password session is sent to the password change.</summary>
    [Theory]
    [MemberData(nameof(Lists))]
    public async Task ARestrictedDean_IsSentToTheForcedChange(string list)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(UrlOf(list, Ls.Needle), ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal(ChangePasswordPath, page.LocationPath);
        Assert.Null(Ls.TagOf(page.Body, FieldOf(list)));
    }

    /// <summary>AC-008, BR-026: searching only reads, so read-only mode does not stop it.</summary>
    [Theory]
    [MemberData(nameof(Lists))]
    public async Task InReadOnlyMode_TheSearchStillWorks(string list)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct, ReadOnlyModeHost.Cause.Suspended);
        await using var _host = host;
        await SeedAsync(host, ct);

        var page = await client.GetAsync(UrlOf(list, Ls.Needle), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(Ls.Needle, Ls.FieldValue(page.Body, FieldOf(list)));
    }

    /// <summary>
    /// AC-008, SC-13: a search calls no Google API, writes no row and no audit event (the control: the search was applied, the
    /// field shows the text).
    /// </summary>
    [Theory]
    [MemberData(nameof(Lists))]
    public async Task ASearch_CallsNoGoogleApi_WritesNothing_AndAuditsNothing(string list)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);
        var before = await host.CourseListFingerprintAsync(ct);

        var page = await client.GetAsync(UrlOf(list, Ls.Needle), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(Ls.Needle, Ls.FieldValue(page.Body, FieldOf(list)));
        Assert.Equal(0, host.GoogleCallCount());
        Assert.Equal(before, await host.CourseListFingerprintAsync(ct));
        var audit = await host.AuditRowsAsJsonAsync(ct);
        Assert.DoesNotContain(audit, row => row.Contains(Ls.Needle, StringComparison.Ordinal));
    }

    /// <summary>
    /// AC-008, SC-10: the searched text, accepted or refused, is in no log line. The control: both requests really ran (the
    /// accepted one shows the text in its field, the refused one answers 400) and the log is not empty.
    /// </summary>
    [Theory]
    [MemberData(nameof(Lists))]
    public async Task TheSearchedText_IsInNoLogLine(string list)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);

        var accepted = await client.GetAsync(UrlOf(list, Ls.Needle), ct);
        Assert.Equal(Ls.Needle, Ls.FieldValue(accepted.Body, FieldOf(list)));
        var refused = await client.GetAsync(PathOf(list) + "?q=" + Ls.Needle + "%07", ct);
        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);

        var events = await host.ReadLogEventsAsync(ct);

        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.DoesNotContain(Ls.Needle, e.Line, StringComparison.Ordinal));
    }
}
