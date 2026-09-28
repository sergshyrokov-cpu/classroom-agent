using System.Runtime.CompilerServices;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Rules;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Classroom.v1;
using Google.Apis.Http;
using Google.Apis.Services;
using GoogleCourse = Google.Apis.Classroom.v1.Data.Course;
using GoogleUserProfile = Google.Apis.Classroom.v1.Data.UserProfile;

namespace ClassroomAgent.Infrastructure.Google;

/// <summary>
/// The Google implementation of <see cref="IClassroomReader"/> (US-014 spec FR-002; entity model §6): the only
/// place the Google SDK types for Classroom courses and rosters exist (AD-4). Every request goes through the
/// injected transport, so nothing but Google is reachable from here (SC-13) and the tests drive it offline
/// (TC-4). The client library's own retries are switched off (retry is US-017, OD-008).
/// </summary>
/// <remarks>
/// <see cref="ReadCoursesAsync"/> reports a course's state as the string Google sent, so the use case — not this
/// adapter — applies OD-010 and skips a value it does not recognise. Every list answer is followed to the end of
/// its continuation token (spec VR-005): the prototype lost student data by fetching a single page.
/// <para>
/// The key is resolved from the secret store for every call and held only for it; nothing about it, no token and
/// no Google error text is logged or returned (SC-7, SC-10). The three scopes used are already on the fixed list
/// of §6 and nothing is added (spec FR-002).
/// </para>
/// </remarks>
public sealed class GoogleClassroomReader : IClassroomReader
{
    private const string ApplicationName = "classroom-agent";

    /// <summary>
    /// How many items one Classroom page asks for. A constant, not a setting: it describes how this program talks
    /// to Google rather than a value a school agreed, so AD-10 does not reach it and DC-3 gains no key (spec
    /// VR-005, I-10). 100 is what Classroom accepts as its maximum for these lists.
    /// </summary>
    private const int PageSize = 100;

    private readonly ISecretStore _secretStore;
    private readonly GoogleServiceAccountSettings _settings;
    private readonly TransportFactory _httpClients;

    public GoogleClassroomReader(
        ISecretStore secretStore,
        GoogleServiceAccountSettings settings,
        HttpMessageHandler transport)
    {
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(transport);
        _secretStore = secretStore;
        _settings = settings;
        _httpClients = new TransportFactory(transport);
    }

    public async IAsyncEnumerable<CourseSnapshot> ReadCoursesAsync(
        string impersonationUser,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(impersonationUser);

        using var service = CreateService(impersonationUser);
        string? pageToken = null;
        do
        {
            // No courseStates filter and no per-user filter (OD-002, OD-003): every course the technical account
            // can see, in whatever state Classroom reports.
            var request = service.Courses.List();
            request.PageSize = PageSize;
            request.PageToken = pageToken;
            var page = await request.ExecuteAsync(cancellationToken);

            foreach (var course in page.Courses ?? [])
            {
                yield return Snapshot(course);
            }

            pageToken = page.NextPageToken;
        }
        while (!string.IsNullOrEmpty(pageToken));
    }

    public async Task<CourseRoster> ReadRosterAsync(
        string impersonationUser,
        string courseGoogleId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(impersonationUser);
        ArgumentException.ThrowIfNullOrWhiteSpace(courseGoogleId);

        using var service = CreateService(impersonationUser);

        // Both rosters in one call, each paged to the end: a returned roster means the read succeeded, and an
        // exception is what an unknown roster looks like (spec FR-004, I-6, I-7). The course owner is not a
        // substitute for the teacher roster — co-teachers are read here (spec FR-004).
        var teachers = await ReadTeachersAsync(service, courseGoogleId, cancellationToken);
        var students = await ReadStudentsAsync(service, courseGoogleId, cancellationToken);
        return new CourseRoster(teachers, students);
    }

    private static async Task<List<RosterEntry>> ReadTeachersAsync(
        ClassroomService service,
        string courseGoogleId,
        CancellationToken cancellationToken)
    {
        var entries = new List<RosterEntry>();
        string? pageToken = null;
        do
        {
            var request = service.Courses.Teachers.List(courseGoogleId);
            request.PageSize = PageSize;
            request.PageToken = pageToken;
            var page = await request.ExecuteAsync(cancellationToken);
            foreach (var teacher in page.Teachers ?? [])
            {
                entries.Add(Entry(teacher.UserId, teacher.Profile));
            }

            pageToken = page.NextPageToken;
        }
        while (!string.IsNullOrEmpty(pageToken));

        return entries;
    }

    private static async Task<List<RosterEntry>> ReadStudentsAsync(
        ClassroomService service,
        string courseGoogleId,
        CancellationToken cancellationToken)
    {
        var entries = new List<RosterEntry>();
        string? pageToken = null;
        do
        {
            var request = service.Courses.Students.List(courseGoogleId);
            request.PageSize = PageSize;
            request.PageToken = pageToken;
            var page = await request.ExecuteAsync(cancellationToken);
            foreach (var student in page.Students ?? [])
            {
                entries.Add(Entry(student.UserId, student.Profile));
            }

            pageToken = page.NextPageToken;
        }
        while (!string.IsNullOrEmpty(pageToken));

        return entries;
    }

    /// <summary>
    /// The course as the Application speaks of it: the state stays the <b>string</b> Google sent, so the use case
    /// can apply OD-010 (entity model §6).
    /// </summary>
    private static CourseSnapshot Snapshot(GoogleCourse course) =>
        new(
            course.Id ?? string.Empty,
            course.CourseState ?? string.Empty,
            new CourseDetails(
                course.Name ?? string.Empty,
                course.Section,
                course.DescriptionHeading,
                course.Description,
                course.Room,
                course.OwnerId,
                course.CreationTimeDateTimeOffset,
                course.UpdateTimeDateTimeOffset,
                course.AlternateLink,
                course.TeacherFolder?.Id,
                course.TeacherFolder?.Title,
                course.CalendarId));

    /// <summary>
    /// One roster entry as Classroom gave it (OD-006): an entry whose profile carries no address is passed on with
    /// the address absent, never discarded and never given a placeholder — the prototype substituted one.
    /// </summary>
    private static RosterEntry Entry(string? userId, GoogleUserProfile? profile) =>
        new(userId ?? profile?.Id ?? string.Empty, profile?.EmailAddress, profile?.Name?.FullName);

    private ClassroomService CreateService(string impersonationUser) =>
        new(new BaseClientService.Initializer
        {
            ApplicationName = ApplicationName,
            HttpClientFactory = _httpClients,
            HttpClientInitializer = Credential(impersonationUser),
            DefaultExponentialBackOffPolicy = ExponentialBackOffPolicy.None,
            GZipEnabled = false,
        });

    /// <summary>
    /// The delegated credential of one call: the school's technical account as the impersonated subject (BR-015,
    /// spec S-03), the three read-only scopes courses and rosters need, and no retry of its own (OD-008).
    /// </summary>
    private ServiceAccountCredential Credential(string impersonationUser)
    {
        var key = LoadKey()
            ?? throw new InvalidOperationException("The service-account key is not available to this installation.");

        return new ServiceAccountCredential(new ServiceAccountCredential.Initializer(key.Id, key.TokenServerUrl)
        {
            Key = key.Key,
            KeyId = key.KeyId,
            User = impersonationUser,
            Scopes =
            [
                GoogleDelegationScopes.CoursesReadonly,
                GoogleDelegationScopes.RostersReadonly,
                GoogleDelegationScopes.ProfileEmails,
            ],
            HttpClientFactory = _httpClients,
            DefaultExponentialBackOffPolicy = ExponentialBackOffPolicy.None,
        });
    }

    /// <summary>
    /// The key the Owner placed at deployment, resolved from the store per call and never stored, logged or
    /// returned (SC-7, US-011 spec FR-016). Nothing about a defective key is echoed, not even its kind of defect.
    /// </summary>
    private ServiceAccountCredential? LoadKey()
    {
        var reference = _settings.KeyReference?.Trim();
        if (string.IsNullOrEmpty(reference) || _secretStore.Resolve(reference) is not { Length: > 0 } json)
        {
            return null;
        }

        try
        {
            var credential = CredentialFactory.FromJson<ServiceAccountCredential>(json);
            return string.IsNullOrWhiteSpace(credential.Id) || credential.Key is null ? null : credential;
        }
        catch (Exception invalid) when (invalid is InvalidOperationException
            or ArgumentException
            or FormatException
            or Newtonsoft.Json.JsonException
            or System.Security.Cryptography.CryptographicException
            or NullReferenceException)
        {
            return null;
        }
    }

    /// <summary>
    /// Hands the one configured transport to the Google client library without letting it dispose it. Unlike
    /// US-011's probe, nothing is classified here: a 5xx or 429 answer is left exactly as the client library sees
    /// it, because this Story neither retries nor diagnoses a Google failure (spec FR-014, OD-008).
    /// </summary>
    private sealed class TransportFactory(HttpMessageHandler transport) : HttpClientFactory
    {
        protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args) => new Borrowed(transport);

        private sealed class Borrowed(HttpMessageHandler inner) : HttpMessageHandler
        {
            private readonly HttpMessageInvoker _invoker = new(inner, disposeHandler: false);

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
                _invoker.SendAsync(request, cancellationToken);

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _invoker.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
