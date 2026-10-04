using System.Net;
using System.Text;
using System.Text.Json;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Infrastructure.Google;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Infrastructure.Google;

/// <summary>
/// US-014 and US-015 spec FR-002, FR-003, FR-004, VR-005: the Google implementation of the Classroom port, driven offline
/// through a scripted transport with a key generated at run time (TC-4). It proves what an Application-layer test
/// cannot — that <b>every page</b> of a list answer is followed, that the impersonated subject is the school's
/// technical account (BR-015), and that a course's state is handed on as the string Google sent so the use case can
/// apply OD-010.
/// </summary>
/// <remarks>
/// The prototype lost student data by fetching a single page (<c>google_api_OLD.py</c> called
/// <c>students().list</c> once), which is why paging is asserted here rather than assumed.
/// </remarks>
public sealed class GoogleClassroomReaderTests
{
    private const string Reference = "installation-google-key";
    private const string TechnicalAccount = AccessCheckTestData.TechnicalAccount;

    private static GoogleClassroomReader ReaderWith(ScriptedHttpHandler transport) =>
        new(
            new DictionarySecretStore(new Dictionary<string, string> { [Reference] = SyntheticServiceAccountKey.Create() }),
            new GoogleServiceAccountSettings(Reference),
            transport,
            new ManualTimeProvider(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero)),
            new FixedRetryJitter(1.0),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleClassroomReader>.Instance);

    private static HttpResponseMessage TokenIssued() =>
        ScriptedHttpHandler.JsonResponse(
            HttpStatusCode.OK,
            """{"access_token":"ya29.synthetic-access-token","expires_in":3599,"token_type":"Bearer"}""");

    /// <summary>One page of <c>courses.list</c>, optionally carrying a continuation token.</summary>
    private static HttpResponseMessage CoursePage(string? nextPageToken, params (string Id, string Name, string State)[] courses) =>
        ScriptedHttpHandler.JsonResponse(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["courses"] = courses.Select(c => new Dictionary<string, object?>
                {
                    ["id"] = c.Id,
                    ["name"] = c.Name,
                    ["courseState"] = c.State,
                }).ToArray(),
                ["nextPageToken"] = nextPageToken,
            }));

    /// <summary>
    /// VR-005: the reader follows the continuation token to the end, so the caller sees every course and never a
    /// page. A single-page implementation fails this test.
    /// </summary>
    [Fact]
    public async Task TheCourseList_IsPagedToTheEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => CoursePage("page-2", (CourseTestData.CourseId(1), CourseTestData.CourseName(1), "ACTIVE")),
            _ => CoursePage(null, (CourseTestData.CourseId(2), CourseTestData.CourseName(2), "ARCHIVED")));
        var reader = ReaderWith(transport);

        var courses = new List<string>();
        await foreach (var course in reader.ReadCoursesAsync(TechnicalAccount, ct))
        {
            courses.Add(course.GoogleId);
        }

        Assert.Equal(new[] { CourseTestData.CourseId(1), CourseTestData.CourseId(2) }, courses);
        var listRequests = transport.Requests
            .Where(r => r.Uri?.AbsolutePath.Contains("/courses", StringComparison.Ordinal) == true)
            .ToList();
        Assert.Equal(2, listRequests.Count);
        Assert.Contains("pageToken=page-2", listRequests[1].Uri!.Query, StringComparison.Ordinal);
    }

    /// <summary>
    /// FR-003, OD-010: the state reaches the caller as the string Google sent. Parsing it into the closed
    /// vocabulary — and skipping a value outside it — is the use case's, not the adapter's.
    /// </summary>
    [Fact]
    public async Task ACourseState_IsHandedOnAsTheStringGoogleSent()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => CoursePage(
                null,
                (CourseTestData.CourseId(1), CourseTestData.CourseName(1), CourseTestData.UnrecognisedState)));
        var reader = ReaderWith(transport);

        var states = new List<string>();
        await foreach (var course in reader.ReadCoursesAsync(TechnicalAccount, ct))
        {
            states.Add(course.State);
        }

        Assert.Equal([CourseTestData.UnrecognisedState], states);
    }

    /// <summary>
    /// FR-002, BR-015, S-03: the delegated token impersonates the school's technical account — never a person and
    /// never a super-admin, which is what the prototype did and what AGENTS.md names as no precedent.
    /// </summary>
    [Fact]
    public async Task TheTokenRequest_ImpersonatesTheTechnicalAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => CoursePage(null, (CourseTestData.CourseId(1), CourseTestData.CourseName(1), "ACTIVE")));
        var reader = ReaderWith(transport);

        await foreach (var _ in reader.ReadCoursesAsync(TechnicalAccount, ct))
        {
            // Draining the sequence is what makes the token request happen.
        }

        var tokenRequest = Assert.Single(transport.Requests, r => r.Uri?.Host == "oauth2.googleapis.com");
        var claims = ClaimsOf(tokenRequest);
        Assert.Equal(TechnicalAccount, claims.GetProperty("sub").GetString());
    }

    /// <summary>
    /// FR-004, VR-005: both rosters of one course are read and each is paged to the end. One call returning both is
    /// what lets the use case tell "roster empty" from "roster unknown" (I-6, I-7).
    /// </summary>
    [Fact]
    public async Task ARoster_ReadsBothListsPagedToTheEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => RosterPage("teachers", "page-2", CourseTestData.UserId(1)),
            _ => RosterPage("teachers", null, CourseTestData.UserId(2)),
            _ => RosterPage("students", null, CourseTestData.UserId(3)));
        var reader = ReaderWith(transport);

        var roster = await reader.ReadRosterAsync(TechnicalAccount, CourseTestData.CourseId(1), ct);

        Assert.Equal(2, roster.Teachers.Count);
        Assert.Single(roster.Students);
        Assert.Equal(
            new[] { CourseTestData.UserId(1), CourseTestData.UserId(2) },
            roster.Teachers.Select(t => t.GoogleUserId));
    }

    /// <summary>
    /// OD-006: a profile Google returns without an address is carried through with the address absent, not
    /// discarded and not replaced by a placeholder — the prototype substituted "ID: &lt;id&gt;", which this design
    /// does not.
    /// </summary>
    [Fact]
    public async Task AProfileWithNoAddress_IsCarriedThroughWithTheAddressAbsent()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, """{"teachers":[]}"""),
            _ => ScriptedHttpHandler.JsonResponse(
                HttpStatusCode.OK,
                JsonSerializer.Serialize(new
                {
                    students = new[]
                    {
                        new { userId = CourseTestData.UserId(1), profile = new { id = CourseTestData.UserId(1) } },
                    },
                })));
        var reader = ReaderWith(transport);

        var roster = await reader.ReadRosterAsync(TechnicalAccount, CourseTestData.CourseId(1), ct);

        var student = Assert.Single(roster.Students);
        Assert.Equal(CourseTestData.UserId(1), student.GoogleUserId);
        Assert.Null(student.Email);
    }

    /// <summary>SC-13: every request this adapter makes goes to Google and nowhere else.</summary>
    [Fact]
    public async Task EveryRequest_GoesToGoogle()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => CoursePage(null, (CourseTestData.CourseId(1), CourseTestData.CourseName(1), "ACTIVE")));
        var reader = ReaderWith(transport);

        await foreach (var _ in reader.ReadCoursesAsync(TechnicalAccount, ct))
        {
            // Drained so the requests happen.
        }

        Assert.All(
            transport.Requests,
            r => Assert.EndsWith("googleapis.com", r.Uri!.Host, StringComparison.Ordinal));
    }

    /// <summary>
    /// US-015 spec FR-003, VR-005: both Classroom resources are read and each is paged to the end; the resource
    /// travels with the item. A single-page implementation fails this test, which the Application-layer paging
    /// tests cannot prove because they run against an in-memory reader.
    /// </summary>
    [Fact]
    public async Task CourseWorkAndMaterials_ArePagedToTheEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => ItemPage("courseWork", "page-2", Item(1)),
            _ => ItemPage("courseWork", null, Item(2)),
            _ => ItemPage("courseWorkMaterial", "page-2", Item(3)),
            _ => ItemPage("courseWorkMaterial", null, Item(4)));
        var reader = ReaderWith(transport);

        var page = await reader.ReadCourseWorkAsync(TechnicalAccount, CourseTestData.CourseId(1), ct);

        Assert.Equal(
            new[]
            {
                (CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork),
                (CourseWorkTestData.ItemId(2), CourseWorkResource.CourseWork),
                (CourseWorkTestData.ItemId(3), CourseWorkResource.CourseWorkMaterial),
                (CourseWorkTestData.ItemId(4), CourseWorkResource.CourseWorkMaterial),
            },
            page.Items.Select(i => (i.GoogleId, i.Resource)));
        var pagedRequests = transport.Requests
            .Where(r => r.Uri?.Query.Contains("pageToken=page-2", StringComparison.Ordinal) == true);
        Assert.Equal(2, pagedRequests.Count());
    }

    /// <summary>US-015 OD-004: only PUBLISHED items are handed on, whatever else Classroom lists.</summary>
    [Fact]
    public async Task OnlyPublishedItems_AreHandedOn()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => ItemPage("courseWork", null, Item(1), Item(2, state: "DRAFT")),
            _ => ItemPage("courseWorkMaterial", null, Item(3, state: "DELETED"), Item(4)));
        var reader = ReaderWith(transport);

        var page = await reader.ReadCourseWorkAsync(TechnicalAccount, CourseTestData.CourseId(1), ct);

        Assert.Equal(
            new[] { CourseWorkTestData.ItemId(1), CourseWorkTestData.ItemId(4) },
            page.Items.Select(i => i.GoogleId));
    }

    /// <summary>
    /// US-015 FR-008, db-design §3.4: the item date is the first of scheduledTime, due date, updateTime and
    /// creationTime; a due date with a time is one UTC instant, and a due date without a time is no due date at all.
    /// </summary>
    [Fact]
    public async Task TheItemDateCascade_AndTheDueDate_AreAppliedInTheAdapter()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => ItemPage(
                "courseWork",
                null,
                Item(1, scheduled: "2026-03-01T08:00:00Z", due: true),
                Item(2, due: true, maxPoints: 12),
                Item(3, dueWithoutTime: true),
                Item(4, updated: null)),
            _ => ItemPage("courseWorkMaterial", null));
        var reader = ReaderWith(transport);

        var items = (await reader.ReadCourseWorkAsync(TechnicalAccount, CourseTestData.CourseId(1), ct)).Items;

        var dueInstant = new DateTimeOffset(2026, 3, 20, 14, 30, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero), items[0].Details.ItemDate);
        Assert.Equal(dueInstant, items[0].Details.DueAt);
        Assert.Equal(dueInstant, items[1].Details.ItemDate);
        Assert.Equal(12m, items[1].Details.MaxPoints);
        Assert.Null(items[2].Details.DueAt);
        Assert.Equal(Updated, items[2].Details.ItemDate);
        Assert.Equal(Created, items[3].Details.ItemDate);
        Assert.Null(items[3].Details.MaxPoints);
    }

    /// <summary>
    /// US-027 spec FR-012, AC-012: the item's <c>scheduledTime</c> is handed on as its own value (the report's lesson
    /// date uses it first); an item without one has none.
    /// </summary>
    [Fact]
    public async Task TheScheduledTime_IsPassedThroughTheAdapter()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => ItemPage("courseWork", null, Item(1, scheduled: "2026-03-01T08:00:00Z"), Item(2)),
            _ => ItemPage("courseWorkMaterial", null));
        var reader = ReaderWith(transport);

        var items = (await reader.ReadCourseWorkAsync(TechnicalAccount, CourseTestData.CourseId(1), ct)).Items;

        Assert.Equal(new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero), items[0].Details.ScheduledTime);
        Assert.Null(items[1].Details.ScheduledTime);
    }

    /// <summary>
    /// US-015 FR-004, OD-002, VR-005: the course's submissions are read once with courseWorkId "-" and paged to the
    /// end; each snapshot keeps its own courseWorkId.
    /// </summary>
    [Fact]
    public async Task Submissions_AreReadOncePerCourse_AndPagedToTheEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => SubmissionPage("page-2", Submission(1, CourseWorkTestData.ItemId(1))),
            _ => SubmissionPage(null, Submission(2, CourseWorkTestData.ItemId(2))));
        var reader = ReaderWith(transport);

        var submissions = await reader.ReadSubmissionsAsync(TechnicalAccount, CourseTestData.CourseId(1), ct);

        Assert.Equal(
            new[] { CourseWorkTestData.ItemId(1), CourseWorkTestData.ItemId(2) },
            submissions.Select(s => s.CourseWorkGoogleId));
        var listRequests = transport.Requests
            .Where(r => r.Uri?.AbsolutePath.EndsWith("/studentSubmissions", StringComparison.Ordinal) == true)
            .ToList();
        Assert.Equal(2, listRequests.Count);
        Assert.All(listRequests, r => Assert.Contains("/courseWork/-/", r.Uri!.AbsolutePath, StringComparison.Ordinal));
    }

    /// <summary>
    /// US-015 FR-009, OD-009: the stored turn-in is the latest transition to TURNED_IN in the history; a submission
    /// with no history has no date, never its update time. The state, grades and late flag pass through as given.
    /// </summary>
    [Fact]
    public async Task TheLastTurnIn_IsTheLatestTurnedInTransition()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => SubmissionPage(
                null,
                Submission(
                    1,
                    CourseWorkTestData.ItemId(1),
                    [
                        ("TURNED_IN", "2026-03-10T09:00:00Z"),
                        ("RECLAIMED_BY_STUDENT", "2026-03-11T09:00:00Z"),
                        ("TURNED_IN", "2026-03-12T09:00:00Z"),
                    ]),
                Submission(2, CourseWorkTestData.ItemId(1))));
        var reader = ReaderWith(transport);

        var submissions = await reader.ReadSubmissionsAsync(TechnicalAccount, CourseTestData.CourseId(1), ct);

        Assert.Equal(new DateTimeOffset(2026, 3, 12, 9, 0, 0, TimeSpan.Zero), submissions[0].TurnedInAt);
        Assert.Equal("TURNED_IN", submissions[0].State);
        Assert.Equal(91.25m, submissions[0].AssignedGrade);
        Assert.True(submissions[0].Late);
        Assert.Null(submissions[1].TurnedInAt);
        Assert.Equal(Updated, submissions[1].UpdateTime);
    }

    /// <summary>
    /// US-015 FR-002, S-04, S-11: the coursework token asks for exactly the two read-only coursework scopes of §6,
    /// on behalf of the technical account (BR-015), and nothing but Google is reached (SC-13).
    /// </summary>
    [Fact]
    public async Task TheCourseWorkToken_AsksOnlyForTheReadOnlyCourseWorkScopes()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => SubmissionPage(null));
        var reader = ReaderWith(transport);

        await reader.ReadSubmissionsAsync(TechnicalAccount, CourseTestData.CourseId(1), ct);

        var tokenRequest = Assert.Single(transport.Requests, r => r.Uri?.Host == "oauth2.googleapis.com");
        var claims = ClaimsOf(tokenRequest);
        Assert.Equal(TechnicalAccount, claims.GetProperty("sub").GetString());
        Assert.Equal(
            new[] { GoogleDelegationScopes.CourseWorkStudentsReadonly, GoogleDelegationScopes.CourseWorkMaterialsReadonly },
            claims.GetProperty("scope").GetString()!.Split(' '));
        Assert.All(
            transport.Requests,
            r => Assert.EndsWith("googleapis.com", r.Uri!.Host, StringComparison.Ordinal));
    }

    private static readonly DateTimeOffset Created = new(2026, 2, 1, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = new(2026, 2, 2, 7, 0, 0, TimeSpan.Zero);

    private static Dictionary<string, object?> Item(
        int ordinal,
        string state = "PUBLISHED",
        string? scheduled = null,
        bool due = false,
        bool dueWithoutTime = false,
        double? maxPoints = null,
        string? updated = "2026-02-02T07:00:00Z")
    {
        var item = new Dictionary<string, object?>
        {
            ["id"] = CourseWorkTestData.ItemId(ordinal),
            ["title"] = CourseWorkTestData.Title(ordinal),
            ["state"] = state,
            ["creationTime"] = "2026-02-01T07:00:00Z",
            ["updateTime"] = updated,
            ["scheduledTime"] = scheduled,
            ["maxPoints"] = maxPoints,
        };
        if (due || dueWithoutTime)
        {
            item["dueDate"] = new Dictionary<string, object?> { ["year"] = 2026, ["month"] = 3, ["day"] = 20 };
        }

        if (due)
        {
            item["dueTime"] = new Dictionary<string, object?> { ["hours"] = 14, ["minutes"] = 30 };
        }

        return item;
    }

    private static HttpResponseMessage ItemPage(
        string collection,
        string? nextPageToken,
        params Dictionary<string, object?>[] items) =>
        ScriptedHttpHandler.JsonResponse(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                [collection] = items,
                ["nextPageToken"] = nextPageToken,
            }));

    private static Dictionary<string, object?> Submission(
        int ordinal,
        string courseWorkId,
        (string State, string At)[]? history = null) =>
        new()
        {
            ["id"] = CourseWorkTestData.SubmissionId(ordinal),
            ["courseWorkId"] = courseWorkId,
            ["userId"] = CourseTestData.UserId(ordinal),
            ["state"] = "TURNED_IN",
            ["assignedGrade"] = 91.25,
            ["late"] = true,
            ["updateTime"] = "2026-02-02T07:00:00Z",
            ["submissionHistory"] = history?.Select(h => new Dictionary<string, object?>
            {
                ["stateHistory"] = new Dictionary<string, object?> { ["state"] = h.State, ["stateTimestamp"] = h.At },
            }).ToArray(),
        };

    private static HttpResponseMessage SubmissionPage(
        string? nextPageToken,
        params Dictionary<string, object?>[] submissions) =>
        ScriptedHttpHandler.JsonResponse(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["studentSubmissions"] = submissions,
                ["nextPageToken"] = nextPageToken,
            }));

    private static HttpResponseMessage RosterPage(string collection, string? nextPageToken, string userId) =>
        ScriptedHttpHandler.JsonResponse(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                [collection] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["userId"] = userId,
                        ["profile"] = new Dictionary<string, object?>
                        {
                            ["id"] = userId,
                            ["name"] = new Dictionary<string, object?> { ["fullName"] = CourseTestData.Name(1) },
                            ["emailAddress"] = CourseTestData.Email("person1"),
                        },
                    },
                },
                ["nextPageToken"] = nextPageToken,
            }));

    private static JsonElement ClaimsOf(ScriptedHttpHandler.RecordedRequest tokenRequest)
    {
        var form = tokenRequest.Body.Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1].Replace('+', ' ')), StringComparer.Ordinal);
        var payload = form["assertion"].Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        return document.RootElement.Clone();
    }
}
