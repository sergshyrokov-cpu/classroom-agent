using System.Globalization;
using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-032 over HTTP, POST side: pick, re-link, remove the mark, confirm and mark "not a course" (spec FR-008 … FR-015;
/// VR-001 … VR-003; AC-008 … AC-012, AC-015; api-design §2.4, §2.5, §2.7). Redirects, stored rows, audit rows, stale
/// state, malformed input, unknown code or course, read-only refusal and the antiforgery token.
/// </summary>
public sealed class MeetCodeActionsHttpTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset Earlier = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private sealed record World(long Alpha, long Beta, long DeanId);

    private static async Task<World> SeedAsync(InstallationTestHost host, CancellationToken ct, Actor actor = Actor.Dean)
    {
        await host.PrepareAsync(ct);
        var alpha = await host.SeedRosterCourseAsync("Test Course Alpha", [Mc.Email("mc.teacher1")], [], ct);
        var beta = await host.SeedRosterCourseAsync("Test Course Beta", [Mc.Email("mc.teacher2")], [], ct);
        return new World(alpha, beta, await host.AccountIdAsync(Mc.EmailOf(actor), ct));
    }

    private static Task<long> MeetingAsync(InstallationTestHost host, string code, CancellationToken ct) =>
        host.SeedMeetingAsync(code, Mc.Email("mc.teacher1"), new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero), ct);

    private static async Task<InstallationAuditRowSummary> OneAuditAsync(InstallationTestHost host, CancellationToken ct)
    {
        var rows = await host.McAuditRowsAsync(ct);
        var row = Assert.Single(rows);
        return new InstallationAuditRowSummary(row);
    }

    private sealed record InstallationAuditRowSummary(Mc.MeetAuditRow Row);

    private static KeyValuePair<string, string>[] LinkForm(long course, string state, long? expected = null, int? returnPage = null) =>
        new[]
        {
            Mc.Field(Mc.Fields.CourseId, course),
            Mc.Field(Mc.Fields.ExpectedState, state),
        }
        .Concat(expected is null ? [] : [Mc.Field(Mc.Fields.ExpectedCourseId, expected)])
        .Concat(returnPage is null ? [] : [Mc.Field(Mc.Fields.ReturnPage, returnPage)])
        .ToArray();

    private static void AssertSucceededAudit(Mc.MeetAuditRow row, string action, long actorId, string code, long? target, long? previous)
    {
        Assert.Equal(action, row.Action);
        Assert.Equal("app_user", row.ActorType);
        Assert.Equal(actorId, row.ActorId);
        Assert.Equal("dean", row.ActorRole);
        Assert.Equal("succeeded", row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.Equal(code, row.MeetCode);
        Assert.Equal(target is null ? null : "course", row.TargetType);
        Assert.Equal(target, row.TargetId);
        Assert.Equal(previous, row.PreviousCourseId);
    }

    /// <summary>AC-008, FR-008: picking a course for an unassigned code links it, unconfirmed, by the Dean; PRG with the message.</summary>
    [Fact]
    public async Task PickingACourse_LinksTheCode_Audits_AndRedirectsWithTheMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await MeetingAsync(host, "act-0001-aaa", ct);

        var response = await client.PostWithTokenAsync(
            Mc.LinkPath("act-0001-aaa"), LinkForm(world.Alpha, Mc.States.Unassigned, returnPage: 2), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/workspace/meet-codes?list=unassigned&page=2", response.LocationPath);
        var link = Assert.Single(await host.McLinkRowsAsync(ct));
        Assert.Equal("act-0001-aaa", link.MeetingCode);
        Assert.Equal(world.Alpha, link.CourseId);
        Assert.False(link.LinkedAutomatically);
        Assert.Equal(world.DeanId, link.LinkedBy);
        Assert.Equal(host.Time.GetUtcNow(), link.LinkedAt);
        Assert.Null(link.ConfirmedBy);
        Assert.Null(link.MarkedBy);
        AssertSucceededAudit((await OneAuditAsync(host, ct)).Row, Mc.Actions.Picked, world.DeanId, "act-0001-aaa", world.Alpha, null);

        var next = await client.GetAsync(response.LocationPath!, ct);
        Assert.Equal(HttpStatusCode.OK, next.Status);
        Assert.Contains(host.Text(Mc.Keys.MessagePicked, "uk"), next.Text, StringComparison.Ordinal);
        var again = await client.GetAsync(response.LocationPath!, ct);
        Assert.DoesNotContain(host.Text(Mc.Keys.MessagePicked, "uk"), again.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-010, FR-010: re-linking A to B moves the code, clears the confirmation, audits old and new course.</summary>
    [Fact]
    public async Task Relinking_MovesTheCode_ClearsTheConfirmation_AndAuditsBothCourses()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await MeetingAsync(host, "act-0002-bbb", ct);
        await host.InsertAppUserAsync(ct, email: Mc.Email("mc.confirmer"));
        var confirmer = await host.AccountIdAsync(Mc.Email("mc.confirmer"), ct);
        await host.SeedAutoLinkAsync("act-0002-bbb", world.Alpha, Earlier, ct, confirmer, Earlier.AddHours(1));

        var response = await client.PostWithTokenAsync(
            Mc.LinkPath("act-0002-bbb"), LinkForm(world.Beta, Mc.States.Linked, world.Alpha, 0), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/workspace/meet-codes?list=linked&page=0", response.LocationPath);
        var link = Assert.Single(await host.McLinkRowsAsync(ct));
        Assert.Equal(world.Beta, link.CourseId);
        Assert.False(link.LinkedAutomatically);
        Assert.Equal(world.DeanId, link.LinkedBy);
        Assert.Equal(host.Time.GetUtcNow(), link.LinkedAt);
        Assert.Null(link.ConfirmedBy);
        Assert.Null(link.ConfirmedAt);
        AssertSucceededAudit((await OneAuditAsync(host, ct)).Row, Mc.Actions.Relinked, world.DeanId, "act-0002-bbb", world.Beta, world.Alpha);
        var next = await client.GetAsync(response.LocationPath!, ct);
        Assert.Contains(host.Text(Mc.Keys.MessageRelinked, "uk"), next.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-012, FR-012: picking a course for a marked code removes the mark.</summary>
    [Fact]
    public async Task PickingACourse_ForAMarkedCode_RemovesTheMark()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await MeetingAsync(host, "act-0003-ccc", ct);
        await host.SeedMarkAsync("act-0003-ccc", world.DeanId, Earlier, ct);

        var response = await client.PostWithTokenAsync(
            Mc.LinkPath("act-0003-ccc"), LinkForm(world.Alpha, Mc.States.Marked, returnPage: 1), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/workspace/meet-codes?list=not-a-course&page=1", response.LocationPath);
        var link = Assert.Single(await host.McLinkRowsAsync(ct));
        Assert.Equal(world.Alpha, link.CourseId);
        Assert.Equal(world.DeanId, link.LinkedBy);
        Assert.Null(link.MarkedBy);
        Assert.Null(link.MarkedAt);
        AssertSucceededAudit((await OneAuditAsync(host, ct)).Row, Mc.Actions.MarkRemoved, world.DeanId, "act-0003-ccc", world.Alpha, null);
        var next = await client.GetAsync(response.LocationPath!, ct);
        Assert.Contains(host.Text(Mc.Keys.MessageMarkRemoved, "uk"), next.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-009, FR-009: confirming records who and when and changes nothing else.</summary>
    [Fact]
    public async Task Confirming_RecordsWhoAndWhen_AndNothingElse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await MeetingAsync(host, "act-0004-ddd", ct);
        await host.SeedAutoLinkAsync("act-0004-ddd", world.Alpha, Earlier, ct);

        var response = await client.PostWithTokenAsync(
            Mc.ConfirmationPath("act-0004-ddd"),
            [Mc.Field(Mc.Fields.ExpectedCourseId, world.Alpha), Mc.Field(Mc.Fields.ReturnPage, 1)],
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/workspace/meet-codes?list=linked&page=1", response.LocationPath);
        var link = Assert.Single(await host.McLinkRowsAsync(ct));
        Assert.Equal(world.Alpha, link.CourseId);
        Assert.True(link.LinkedAutomatically);
        Assert.Null(link.LinkedBy);
        Assert.Equal(Earlier, link.LinkedAt);
        Assert.Equal(world.DeanId, link.ConfirmedBy);
        Assert.Equal(host.Time.GetUtcNow(), link.ConfirmedAt);
        AssertSucceededAudit((await OneAuditAsync(host, ct)).Row, Mc.Actions.Confirmed, world.DeanId, "act-0004-ddd", world.Alpha, null);
        var next = await client.GetAsync(response.LocationPath!, ct);
        Assert.Contains(host.Text(Mc.Keys.MessageConfirmed, "uk"), next.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-011, FR-011: marking a linked code clears the course and records the previous one.</summary>
    [Fact]
    public async Task MarkingALinkedCode_ClearsTheCourse_AndAuditsThePreviousCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await MeetingAsync(host, "act-0005-eee", ct);
        await host.SeedAutoLinkAsync("act-0005-eee", world.Alpha, Earlier, ct);

        var response = await client.PostWithTokenAsync(
            Mc.MarkPath("act-0005-eee"),
            [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Linked), Mc.Field(Mc.Fields.ExpectedCourseId, world.Alpha), Mc.Field(Mc.Fields.ReturnPage, 0)],
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/workspace/meet-codes?list=linked&page=0", response.LocationPath);
        var link = Assert.Single(await host.McLinkRowsAsync(ct));
        Assert.Null(link.CourseId);
        Assert.Null(link.LinkedAutomatically);
        Assert.Null(link.LinkedBy);
        Assert.Null(link.ConfirmedBy);
        Assert.Equal(world.DeanId, link.MarkedBy);
        Assert.Equal(host.Time.GetUtcNow(), link.MarkedAt);
        AssertSucceededAudit((await OneAuditAsync(host, ct)).Row, Mc.Actions.Marked, world.DeanId, "act-0005-eee", null, world.Alpha);
        var next = await client.GetAsync(response.LocationPath!, ct);
        Assert.Contains(host.Text(Mc.Keys.MessageMarked, "uk"), next.Text, StringComparison.Ordinal);
    }

    /// <summary>FR-011: marking an unassigned code inserts the mark; there is no previous course.</summary>
    [Fact]
    public async Task MarkingAnUnassignedCode_StoresTheMark_WithNoPreviousCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await MeetingAsync(host, "act-0006-fff", ct);

        var response = await client.PostWithTokenAsync(
            Mc.MarkPath("act-0006-fff"), [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned)], ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/workspace/meet-codes?list=unassigned&page=0", response.LocationPath);
        var link = Assert.Single(await host.McLinkRowsAsync(ct));
        Assert.Null(link.CourseId);
        Assert.Equal(world.DeanId, link.MarkedBy);
        AssertSucceededAudit((await OneAuditAsync(host, ct)).Row, Mc.Actions.Marked, world.DeanId, "act-0006-fff", null, null);
    }

    /// <summary>The eight stale shapes of FR-013.</summary>
    public static TheoryData<string> StaleCases => new(
        "link-unassigned-but-linked",
        "link-linked-wrong-course",
        "link-marked-but-linked",
        "link-unassigned-but-marked",
        "confirm-already-confirmed",
        "confirm-wrong-course",
        "mark-unassigned-but-linked",
        "mark-linked-wrong-course");

    /// <summary>FR-013, AC-015: a stale screen is 409 with StateChanged; nothing changes, no audit row.</summary>
    [Theory]
    [MemberData(nameof(StaleCases))]
    public async Task AStaleState_Is409_WithStateChanged_AndNothingChanges(string scenario)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await MeetingAsync(host, "stl-0001-aaa", ct);
        await MeetingAsync(host, "stl-0002-bbb", ct);
        await host.SeedAutoLinkAsync("stl-0001-aaa", world.Alpha, Earlier, ct, world.DeanId, Earlier.AddHours(1));
        await host.SeedMarkAsync("stl-0002-bbb", world.DeanId, Earlier, ct);
        (string path, KeyValuePair<string, string>[] fields) = scenario switch
        {
            "link-unassigned-but-linked" => (Mc.LinkPath("stl-0001-aaa"), LinkForm(world.Beta, Mc.States.Unassigned)),
            "link-linked-wrong-course" => (Mc.LinkPath("stl-0001-aaa"), LinkForm(world.Alpha, Mc.States.Linked, world.Beta)),
            "link-marked-but-linked" => (Mc.LinkPath("stl-0001-aaa"), LinkForm(world.Beta, Mc.States.Marked)),
            "link-unassigned-but-marked" => (Mc.LinkPath("stl-0002-bbb"), LinkForm(world.Beta, Mc.States.Unassigned)),
            "confirm-already-confirmed" => (Mc.ConfirmationPath("stl-0001-aaa"), [Mc.Field(Mc.Fields.ExpectedCourseId, world.Alpha)]),
            "confirm-wrong-course" => (Mc.ConfirmationPath("stl-0001-aaa"), [Mc.Field(Mc.Fields.ExpectedCourseId, world.Beta)]),
            "mark-unassigned-but-linked" => (Mc.MarkPath("stl-0001-aaa"), [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned)]),
            "mark-linked-wrong-course" => (Mc.MarkPath("stl-0001-aaa"),
                [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Linked), Mc.Field(Mc.Fields.ExpectedCourseId, world.Beta)]),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
        var before = await host.McLinksFingerprintAsync(ct);

        var response = await client.PostWithTokenAsync(path, fields, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Contains(host.Text(Mc.Keys.MessageStateChanged, "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.McLinksFingerprintAsync(ct));
        Assert.Empty(await host.McAuditRowsAsync(ct));
    }

    /// <summary>VR-001 … VR-003: malformed input is 400 with the Error.PageExpired text; nothing is written.</summary>
    [Theory]
    [InlineData("link-course-not-a-number")]
    [InlineData("link-course-zero")]
    [InlineData("link-course-repeated")]
    [InlineData("link-state-unknown")]
    [InlineData("link-state-wrong-case")]
    [InlineData("link-linked-without-course")]
    [InlineData("link-unassigned-with-course")]
    [InlineData("confirm-without-course")]
    [InlineData("mark-state-marked")]
    [InlineData("mark-linked-without-course")]
    public async Task MalformedInput_Is400_WithPageExpired_AndNothingIsWritten(string scenario)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await MeetingAsync(host, "bad-0001-aaa", ct);
        var a = Id(world.Alpha);
        (string path, KeyValuePair<string, string>[] fields) = scenario switch
        {
            "link-course-not-a-number" => (Mc.LinkPath("bad-0001-aaa"), Pairs(("courseId", "abc"), ("expectedState", "unassigned"))),
            "link-course-zero" => (Mc.LinkPath("bad-0001-aaa"), Pairs(("courseId", "0"), ("expectedState", "unassigned"))),
            "link-course-repeated" => (Mc.LinkPath("bad-0001-aaa"), Pairs(("courseId", a), ("courseId", a), ("expectedState", "unassigned"))),
            "link-state-unknown" => (Mc.LinkPath("bad-0001-aaa"), Pairs(("courseId", a), ("expectedState", "bogus"))),
            "link-state-wrong-case" => (Mc.LinkPath("bad-0001-aaa"), Pairs(("courseId", a), ("expectedState", "Unassigned"))),
            "link-linked-without-course" => (Mc.LinkPath("bad-0001-aaa"), Pairs(("courseId", a), ("expectedState", "linked"))),
            "link-unassigned-with-course" => (Mc.LinkPath("bad-0001-aaa"), Pairs(("courseId", a), ("expectedState", "unassigned"), ("expectedCourseId", a))),
            "confirm-without-course" => (Mc.ConfirmationPath("bad-0001-aaa"), Pairs()),
            "mark-state-marked" => (Mc.MarkPath("bad-0001-aaa"), Pairs(("expectedState", "marked"))),
            "mark-linked-without-course" => (Mc.MarkPath("bad-0001-aaa"), Pairs(("expectedState", "linked"))),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };

        var response = await client.PostWithTokenAsync(path, fields, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text(SignInTestData.TextKeys.PageExpired, "uk"), response.Text, StringComparison.Ordinal);
        Assert.Empty(await host.McLinkRowsAsync(ct));
        Assert.Empty(await host.McAuditRowsAsync(ct));
    }

    private static KeyValuePair<string, string>[] Pairs(params (string Name, string Value)[] fields) =>
        fields.Select(f => new KeyValuePair<string, string>(f.Name, f.Value)).ToArray();

    /// <summary>VR-002: re-linking to the course the code already names is 400 with the SameCourse field error.</summary>
    [Fact]
    public async Task RelinkingToTheCurrentCourse_Is400_WithSameCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        await MeetingAsync(host, "same-0001-aaa", ct);
        await host.SeedAutoLinkAsync("same-0001-aaa", world.Alpha, Earlier, ct);
        var before = await host.McLinksFingerprintAsync(ct);

        var response = await client.PostWithTokenAsync(
            Mc.LinkPath("same-0001-aaa"), LinkForm(world.Alpha, Mc.States.Linked, world.Alpha), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text(Mc.Keys.FieldSameCourse, "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.McLinksFingerprintAsync(ct));
        Assert.Empty(await host.McAuditRowsAsync(ct));
    }

    /// <summary>VR-001: a well-formed code with no link and no meeting is 404 with CodeNotFound, on every write.</summary>
    [Theory]
    [InlineData("link")]
    [InlineData("confirmation")]
    [InlineData("mark")]
    public async Task AnUnknownCode_Is404_WithCodeNotFound(string operation)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        const string code = "zzz-9999-nnn";
        (string path, KeyValuePair<string, string>[] fields) = operation switch
        {
            "link" => (Mc.LinkPath(code), LinkForm(world.Alpha, Mc.States.Unassigned)),
            "confirmation" => (Mc.ConfirmationPath(code), [Mc.Field(Mc.Fields.ExpectedCourseId, world.Alpha)]),
            _ => (Mc.MarkPath(code), [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned)]),
        };

        var response = await client.PostWithTokenAsync(path, fields, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Contains(host.Text(Mc.Keys.MessageCodeNotFound, "uk"), response.Text, StringComparison.Ordinal);
        Assert.Empty(await host.McLinkRowsAsync(ct));
        Assert.Empty(await host.McAuditRowsAsync(ct));
    }

    /// <summary>VR-002: an unknown course id is 404 with the CourseNotFound field error; nothing is written.</summary>
    [Fact]
    public async Task AnUnknownCourse_Is404_WithCourseNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        await SeedAsync(host, ct);
        await MeetingAsync(host, "unk-0001-aaa", ct);

        var response = await client.PostWithTokenAsync(
            Mc.LinkPath("unk-0001-aaa"), LinkForm(999_999, Mc.States.Unassigned), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Contains(host.Text(Mc.Keys.FieldCourseNotFound, "uk"), response.Text, StringComparison.Ordinal);
        Assert.Empty(await host.McLinkRowsAsync(ct));
        Assert.Empty(await host.McAuditRowsAsync(ct));
    }

    public static TheoryData<ReadOnlyModeHost.Cause, string> ReadOnlyCases
    {
        get
        {
            var data = new TheoryData<ReadOnlyModeHost.Cause, string>();
            foreach (var cause in new[] { ReadOnlyModeHost.Cause.NeverConfirmed, ReadOnlyModeHost.Cause.Suspended, ReadOnlyModeHost.Cause.GracePeriodExpired })
            {
                foreach (var operation in new[] { "link", "confirmation", "mark" })
                {
                    data.Add(cause, operation);
                }
            }

            return data;
        }
    }

    /// <summary>FR-015, AC-015: in every read-only cause each write is 409 with the reason, changes nothing, and writes one refused row.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyCases))]
    public async Task InReadOnlyMode_EachWriteIsRefused_WithTheReason_AndOneRefusedAuditRow(ReadOnlyModeHost.Cause cause, string operation)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct, cause);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        const string code = "ro-0001-aaa";
        await MeetingAsync(host, code, ct);
        if (operation == "confirmation")
        {
            await host.SeedAutoLinkAsync(code, world.Alpha, Earlier, ct);
        }

        (string path, KeyValuePair<string, string>[] fields, string action) = operation switch
        {
            "link" => (Mc.LinkPath(code), LinkForm(world.Alpha, Mc.States.Unassigned), Mc.Actions.Picked),
            "confirmation" => (Mc.ConfirmationPath(code), [Mc.Field(Mc.Fields.ExpectedCourseId, world.Alpha)], Mc.Actions.Confirmed),
            _ => (Mc.MarkPath(code), [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned)], Mc.Actions.Marked),
        };
        var before = await host.McLinksFingerprintAsync(ct);

        var response = await client.PostWithTokenAsync(path, fields, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Contains(host.Text(Mc.ReasonKeyOf(cause), "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.McLinksFingerprintAsync(ct));
        var row = Assert.Single(await host.McAuditRowsAsync(ct));
        Assert.Equal(action, row.Action);
        Assert.Equal("refused", row.Outcome);
        Assert.Equal("read_only_mode", row.RefusalCategory);
        Assert.Equal(world.DeanId, row.ActorId);
        Assert.Null(row.TargetType);
        Assert.Null(row.TargetId);
        Assert.Null(row.PreviousCourseId);
        Assert.Equal(code, row.MeetCode);
    }

    /// <summary>API-7: a write without the antiforgery token is 400 with the Error.PageExpired text and stores nothing.</summary>
    [Theory]
    [InlineData("link")]
    [InlineData("confirmation")]
    [InlineData("mark")]
    public async Task AWriteWithoutTheAntiforgeryToken_Is400(string operation)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        var world = await SeedAsync(host, ct);
        const string code = "csrf-0001-aaa";
        await MeetingAsync(host, code, ct);
        await host.SeedAutoLinkAsync("csrf-0002-bbb", world.Alpha, Earlier, ct);
        var before = await host.McLinksFingerprintAsync(ct);
        (string path, KeyValuePair<string, string>[] fields) = operation switch
        {
            "link" => (Mc.LinkPath(code), LinkForm(world.Alpha, Mc.States.Unassigned)),
            "confirmation" => (Mc.ConfirmationPath("csrf-0002-bbb"), [Mc.Field(Mc.Fields.ExpectedCourseId, world.Alpha)]),
            _ => (Mc.MarkPath(code), [Mc.Field(Mc.Fields.ExpectedState, Mc.States.Unassigned)]),
        };

        var response = await client.PostWithTokenAsync(path, fields, ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text(SignInTestData.TextKeys.PageExpired, "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.McLinksFingerprintAsync(ct));
        Assert.Empty(await host.McAuditRowsAsync(ct));
    }
}
