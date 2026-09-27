using System.Net;
using System.Text;
using System.Text.Json;
using ClassroomAgent.Infrastructure.Google;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Infrastructure.Google;

/// <summary>
/// US-014 spec FR-002, FR-003, FR-004, VR-005: the Google implementation of the Classroom port, driven offline
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
            transport);

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
