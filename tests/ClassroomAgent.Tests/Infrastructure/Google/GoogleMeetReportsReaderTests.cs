using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Infrastructure.Google;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Infrastructure.Google;

/// <summary>
/// US-031 AC-015, spec FR-003, FR-004, BR-015, SC-8, OD-009: the Google implementation of the Meet events port, driven
/// offline through a scripted transport with a key generated at run time (TC-4). It proves what an Application-layer
/// test cannot — the shape of the <c>activities.list</c> request, that <b>every page</b> is followed, the single mapping
/// of an event to the few values the model keeps, the impersonated subject and the one read-only scope.
/// </summary>
public sealed class GoogleMeetReportsReaderTests
{
    private const string Reference = "installation-google-key";
    private const string TechnicalAccount = AccessCheckTestData.TechnicalAccount;
    private const string TokenHost = "oauth2.googleapis.com";
    private const string DisplayName = "Synthetic Display Name";
    private const string IpAddress = "192.0.2.10";
    private const string Country = "ZZ";

    private static readonly DateTimeOffset From = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 9, 17, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset At = new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    private static GoogleMeetReportsReader ReaderWith(ScriptedHttpHandler transport) =>
        new(
            new DictionarySecretStore(new Dictionary<string, string> { [Reference] = SyntheticServiceAccountKey.Create() }),
            new GoogleServiceAccountSettings(Reference),
            transport,
            new ManualTimeProvider(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero)),
            new FixedRetryJitter(1.0),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleMeetReportsReader>.Instance);

    private static HttpResponseMessage TokenIssued() =>
        ScriptedHttpHandler.JsonResponse(
            HttpStatusCode.OK,
            """{"access_token":"ya29.synthetic-access-token","expires_in":3599,"token_type":"Bearer"}""");

    private static async Task<List<MeetEventPage>> ReadAllAsync(GoogleMeetReportsReader reader, CancellationToken ct)
    {
        var pages = new List<MeetEventPage>();
        await foreach (var page in reader.ReadCallEndedAsync(TechnicalAccount, From, To, ct))
        {
            pages.Add(page);
        }

        return pages;
    }

    /// <summary>One <c>activities.list</c> answer with a <c>call_ended</c> activity per parameter set.</summary>
    private static HttpResponseMessage ActivityPage(string? nextPageToken, params object[][] events) =>
        ScriptedHttpHandler.JsonResponse(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["kind"] = "admin#reports#activities",
                ["items"] = events.Select((parameters, i) => new Dictionary<string, object?>
                {
                    ["kind"] = "admin#reports#activity",
                    ["id"] = new Dictionary<string, object?>
                    {
                        ["time"] = At.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                        ["uniqueQualifier"] = (i + 1).ToString(CultureInfo.InvariantCulture),
                        ["applicationName"] = "meet",
                        ["customerId"] = "C0synthetic",
                    },
                    ["events"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["type"] = "call",
                            ["name"] = "call_ended",
                            ["parameters"] = parameters,
                        },
                    },
                }).ToArray(),
                ["nextPageToken"] = nextPageToken,
            }));

    private static Dictionary<string, object?> Text(string name, string value) =>
        new() { ["name"] = name, ["value"] = value };

    /// <summary>The parameters of one full event, with Google's extra telemetry the model must never keep.</summary>
    private static object[] FullEvent(int conference, int endpoint, string? organizer, string? participant) =>
        new[]
        {
            Text("conference_id", MeetTestData.ConferenceId(conference)),
            Text("meeting_code", MeetTestData.MeetingCode(conference)),
            organizer is null ? null : Text("organizer_email", organizer),
            Text("endpoint_id", MeetTestData.EndpointId(endpoint)),
            participant is null ? null : Text("identifier", participant),
            participant is null ? null : Text("identifier_type", MeetTestData.EmailIdentifierType),
            new Dictionary<string, object?> { ["name"] = "duration_seconds", ["intValue"] = "3600" },
            Text("display_name", DisplayName),
            new Dictionary<string, object?> { ["name"] = "is_external", ["boolValue"] = false },
            Text("ip_address", IpAddress),
            Text("device_type", "web"),
            Text("location_country", Country),
        }.Where(p => p is not null).Cast<object>().ToArray();

    /// <summary>
    /// FR-003: the request is <c>activities.list</c> for user <c>all</c>, application <c>meet</c>, event
    /// <c>call_ended</c>, over the window handed in.
    /// </summary>
    [Fact]
    public async Task TheRequest_IsActivitiesListForAllUsersAndMeet_OverTheWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(_ => TokenIssued(), _ => ActivityPage(null));
        var reader = ReaderWith(transport);

        await ReadAllAsync(reader, ct);

        var request = Assert.Single(transport.Requests, r => r.Uri?.Host != TokenHost);
        Assert.EndsWith(
            "/admin/reports/v1/activity/users/all/applications/meet",
            request.Uri!.AbsolutePath,
            StringComparison.Ordinal);
        var query = HttpUtility.ParseQueryString(request.Uri.Query);
        Assert.Equal("call_ended", query["eventName"]);
        Assert.Equal(From, DateTimeOffset.Parse(query["startTime"]!, CultureInfo.InvariantCulture));
        Assert.Equal(To, DateTimeOffset.Parse(query["endTime"]!, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// FR-004: the reader follows the continuation token to the end, so the caller sees every event and never a page. A
    /// single-page implementation fails this test.
    /// </summary>
    [Fact]
    public async Task EveryPage_IsFollowed_InOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => ActivityPage("page-2", FullEvent(1, 1, MeetTestData.Teacher(1), MeetTestData.Student(1))),
            _ => ActivityPage(null, FullEvent(2, 2, MeetTestData.Teacher(2), MeetTestData.Student(2))));
        var reader = ReaderWith(transport);

        var pages = await ReadAllAsync(reader, ct);

        Assert.Equal(2, pages.Count);
        Assert.Equal(MeetTestData.ConferenceId(1), Assert.Single(pages[0].Events).ConferenceId);
        Assert.Equal(MeetTestData.ConferenceId(2), Assert.Single(pages[1].Events).ConferenceId);
        var apiRequests = transport.Requests.Where(r => r.Uri?.Host != TokenHost).ToList();
        Assert.Equal(2, apiRequests.Count);
        Assert.Equal("page-2", HttpUtility.ParseQueryString(apiRequests[1].Uri!.Query)["pageToken"]);
    }

    /// <summary>FR-003, OD-009: one event maps to the model with the values Google sent, and nothing is invented.</summary>
    [Fact]
    public async Task AnEvent_IsMappedToTheModel()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => ActivityPage(null, FullEvent(1, 3, MeetTestData.Teacher(1), MeetTestData.Student(1))));
        var reader = ReaderWith(transport);

        var pages = await ReadAllAsync(reader, ct);

        var mapped = Assert.Single(Assert.Single(pages).Events);
        Assert.Equal(At, mapped.OccurredAt);
        Assert.Equal(MeetTestData.ConferenceId(1), mapped.ConferenceId);
        Assert.Equal(MeetTestData.MeetingCode(1), mapped.MeetingCode);
        Assert.Equal(MeetTestData.Teacher(1), mapped.OrganizerEmail);
        Assert.Equal(MeetTestData.EndpointId(3), mapped.EndpointId);
        Assert.Equal(MeetTestData.Student(1), mapped.Identifier);
        Assert.Equal(MeetTestData.EmailIdentifierType, mapped.IdentifierType);
        Assert.Equal(3600L, mapped.DurationSeconds);
    }

    /// <summary>
    /// AC-013: nothing else leaves the adapter. The scripted body really holds the display name, the address and the
    /// country (asserted), so the absence below is not vacuous.
    /// </summary>
    [Fact]
    public async Task NothingElse_LeavesTheAdapter()
    {
        var ct = TestContext.Current.CancellationToken;
        var scripted = ActivityPage(null, FullEvent(1, 1, MeetTestData.Teacher(1), MeetTestData.Student(1)));
        var scriptedBody = await scripted.Content.ReadAsStringAsync(ct);
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => ActivityPage(null, FullEvent(1, 1, MeetTestData.Teacher(1), MeetTestData.Student(1))));
        var reader = ReaderWith(transport);

        var pages = await ReadAllAsync(reader, ct);

        Assert.Contains(DisplayName, scriptedBody, StringComparison.Ordinal);
        Assert.Contains(IpAddress, scriptedBody, StringComparison.Ordinal);
        Assert.Contains(Country, scriptedBody, StringComparison.Ordinal);
        var text = Assert.Single(Assert.Single(pages).Events).ToString();
        Assert.DoesNotContain(DisplayName, text, StringComparison.Ordinal);
        Assert.DoesNotContain(IpAddress, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Country, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// FR-003: optional parameters Google omits come back as absent values; the event is still returned and the read
    /// does not throw — judging the event is the use case's (VR-001).
    /// </summary>
    [Fact]
    public async Task MissingOptionalParameters_AreAbsentValues_AndTheEventIsStillReturned()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => ActivityPage(null, FullEvent(1, 1, null, null)));
        var reader = ReaderWith(transport);

        var pages = await ReadAllAsync(reader, ct);

        var mapped = Assert.Single(Assert.Single(pages).Events);
        Assert.Null(mapped.OrganizerEmail);
        Assert.Null(mapped.Identifier);
        Assert.Null(mapped.IdentifierType);
        Assert.Equal(MeetTestData.ConferenceId(1), mapped.ConferenceId);
    }

    /// <summary>
    /// BR-015, SC-8, AC-015: the delegated token impersonates the school's technical account and asks for exactly the
    /// one read-only Admin Reports scope — no other.
    /// </summary>
    [Fact]
    public async Task TheTokenRequest_ImpersonatesTheTechnicalAccount_WithTheReportsScopeOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(_ => TokenIssued(), _ => ActivityPage(null));
        var reader = ReaderWith(transport);

        await ReadAllAsync(reader, ct);

        var tokenRequest = Assert.Single(transport.Requests, r => r.Uri?.Host == TokenHost);
        var claims = ClaimsOf(tokenRequest);
        Assert.Equal(TechnicalAccount, claims.GetProperty("sub").GetString());
        Assert.Equal(GoogleDelegationScopes.AdminReportsAuditReadonly, claims.GetProperty("scope").GetString());
    }

    /// <summary>NFR-021, BR-030, SC-8: the adapter only reads — every request to Google other than the token one is a GET.</summary>
    [Fact]
    public async Task EveryApiRequest_IsAGet()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => ActivityPage("page-2", FullEvent(1, 1, MeetTestData.Teacher(1), MeetTestData.Student(1))),
            _ => ActivityPage(null, FullEvent(2, 2, MeetTestData.Teacher(2), MeetTestData.Student(2))));
        var reader = ReaderWith(transport);

        await ReadAllAsync(reader, ct);

        var apiRequests = transport.Requests.Where(r => r.Uri?.Host != TokenHost).ToList();
        Assert.NotEmpty(apiRequests);
        Assert.All(apiRequests, r =>
        {
            Assert.EndsWith("googleapis.com", r.Uri!.Host, StringComparison.Ordinal);
            Assert.Equal(HttpMethod.Get, r.Method);
        });
    }

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
