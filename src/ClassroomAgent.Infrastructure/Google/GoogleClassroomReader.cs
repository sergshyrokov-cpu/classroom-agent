using System.Runtime.CompilerServices;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Classroom.v1;
using Google.Apis.Http;
using Google.Apis.Services;
using GoogleCourse = Google.Apis.Classroom.v1.Data.Course;
using GoogleCourseWork = Google.Apis.Classroom.v1.Data.CourseWork;
using GoogleCourseWorkMaterial = Google.Apis.Classroom.v1.Data.CourseWorkMaterial;
using GoogleDate = Google.Apis.Classroom.v1.Data.Date;
using GoogleSubmission = Google.Apis.Classroom.v1.Data.StudentSubmission;
using GoogleSubmissionHistory = Google.Apis.Classroom.v1.Data.SubmissionHistory;
using GoogleTimeOfDay = Google.Apis.Classroom.v1.Data.TimeOfDay;
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
/// no Google error text is logged or returned (SC-7, SC-10). Each call asks only for the read-only scopes it needs, all already on the fixed list
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

    /// <summary>Classroom's wildcard for "every piece of coursework of the course" (OD-002).</summary>
    private const string AllCourseWork = "-";

    private const string PublishedState = "PUBLISHED";
    private const string TurnedInState = "TURNED_IN";

    /// <summary>The three read-only scopes courses and rosters need (US-014).</summary>
    private static readonly string[] CourseAndRosterScopes =
    [
        GoogleDelegationScopes.CoursesReadonly,
        GoogleDelegationScopes.RostersReadonly,
        GoogleDelegationScopes.ProfileEmails,
    ];

    /// <summary>
    /// The two read-only scopes coursework, materials and submissions need — both already among the six of §6, so
    /// nothing is added to the delegation (US-015 spec FR-002, S-04).
    /// </summary>
    private static readonly string[] CourseWorkScopes =
    [
        GoogleDelegationScopes.CourseWorkStudentsReadonly,
        GoogleDelegationScopes.CourseWorkMaterialsReadonly,
    ];

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

    public async Task<CourseWorkPage> ReadCourseWorkAsync(
        string impersonationUser,
        string courseGoogleId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(impersonationUser);
        ArgumentException.ThrowIfNullOrWhiteSpace(courseGoogleId);

        using var service = CreateService(impersonationUser, CourseWorkScopes);

        // Both resources in one call, each paged to the end: a returned page means both reads succeeded, and an
        // exception is what unknown items look like (US-015 entity model §6, FR-003, VR-005).
        var items = new List<CourseWorkSnapshot>();
        await ReadAssignmentsAsync(service, courseGoogleId, items, cancellationToken);
        await ReadMaterialsAsync(service, courseGoogleId, items, cancellationToken);
        return new CourseWorkPage(items);
    }

    public async Task<IReadOnlyList<SubmissionSnapshot>> ReadSubmissionsAsync(
        string impersonationUser,
        string courseGoogleId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(impersonationUser);
        ArgumentException.ThrowIfNullOrWhiteSpace(courseGoogleId);

        using var service = CreateService(impersonationUser, CourseWorkScopes);

        // Once per course with courseWorkId "-" (OD-002), so the call count stays linear in courses (NFR-002).
        // Materials have no submissions and none are asked for (I-6).
        var snapshots = new List<SubmissionSnapshot>();
        string? pageToken = null;
        do
        {
            var request = service.Courses.CourseWork.StudentSubmissions.List(courseGoogleId, AllCourseWork);
            request.PageSize = PageSize;
            request.PageToken = pageToken;
            var page = await request.ExecuteAsync(cancellationToken);
            foreach (var submission in page.StudentSubmissions ?? [])
            {
                snapshots.Add(Snapshot(submission));
            }

            pageToken = page.NextPageToken;
        }
        while (!string.IsNullOrEmpty(pageToken));

        return snapshots;
    }

    private static async Task ReadAssignmentsAsync(
        ClassroomService service,
        string courseGoogleId,
        List<CourseWorkSnapshot> items,
        CancellationToken cancellationToken)
    {
        string? pageToken = null;
        do
        {
            var request = service.Courses.CourseWork.List(courseGoogleId);
            request.PageSize = PageSize;
            request.PageToken = pageToken;
            var page = await request.ExecuteAsync(cancellationToken);
            foreach (var work in page.CourseWork ?? [])
            {
                // Only PUBLISHED items are imported and the state is not stored (OD-004). Classroom's default
                // already lists only those; the check keeps the rule here rather than in an assumption.
                if (work.State == PublishedState)
                {
                    items.Add(Snapshot(work));
                }
            }

            pageToken = page.NextPageToken;
        }
        while (!string.IsNullOrEmpty(pageToken));
    }

    private static async Task ReadMaterialsAsync(
        ClassroomService service,
        string courseGoogleId,
        List<CourseWorkSnapshot> items,
        CancellationToken cancellationToken)
    {
        string? pageToken = null;
        do
        {
            var request = service.Courses.CourseWorkMaterials.List(courseGoogleId);
            request.PageSize = PageSize;
            request.PageToken = pageToken;
            var page = await request.ExecuteAsync(cancellationToken);
            foreach (var material in page.CourseWorkMaterial ?? [])
            {
                if (material.State == PublishedState)
                {
                    items.Add(Snapshot(material));
                }
            }

            pageToken = page.NextPageToken;
        }
        while (!string.IsNullOrEmpty(pageToken));
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
    /// A piece of coursework as the Application speaks of it. The FR-008 cascade is applied here, because it reduces
    /// four Google fields to one (entity model §6). A missing id, title or cascade date is handed on as absent and
    /// refused by the entity (VR-002, db-design §3.3), never patched here.
    /// </summary>
    private static CourseWorkSnapshot Snapshot(GoogleCourseWork work)
    {
        var dueAt = DueInstant(work.DueDate, work.DueTime);
        return new CourseWorkSnapshot(
            work.Id ?? string.Empty,
            CourseWorkResource.CourseWork,
            new CourseWorkDetails(
                work.Title ?? string.Empty,
                ItemDate(work.ScheduledTimeDateTimeOffset, dueAt, work.UpdateTimeDateTimeOffset, work.CreationTimeDateTimeOffset),
                dueAt,
                work.MaxPoints is { } maxPoints ? (decimal)maxPoints : null,
                work.CreationTimeDateTimeOffset,
                work.UpdateTimeDateTimeOffset));
    }

    /// <summary>A material: the same cascade with no due date, and no points (§3, BR-052).</summary>
    private static CourseWorkSnapshot Snapshot(GoogleCourseWorkMaterial material) =>
        new(
            material.Id ?? string.Empty,
            CourseWorkResource.CourseWorkMaterial,
            new CourseWorkDetails(
                material.Title ?? string.Empty,
                ItemDate(material.ScheduledTimeDateTimeOffset, null, material.UpdateTimeDateTimeOffset, material.CreationTimeDateTimeOffset),
                null,
                null,
                material.CreationTimeDateTimeOffset,
                material.UpdateTimeDateTimeOffset));

    /// <summary>
    /// A submission as the Application speaks of it (entity model §6). The history is reduced here to its latest
    /// transition to <c>TURNED_IN</c> and discarded, so no Application or Domain type can ever hold it (FR-009,
    /// PC-13); a history Google omitted gives no date, never one inferred from the update time (OD-009). The state
    /// stays the string Google sent, for the use case to classify (OD-005, OD-011).
    /// </summary>
    private static SubmissionSnapshot Snapshot(GoogleSubmission submission) =>
        new(
            submission.Id ?? string.Empty,
            submission.CourseWorkId ?? string.Empty,
            submission.UserId ?? string.Empty,
            submission.State ?? string.Empty,
            submission.AssignedGrade is { } assigned ? (decimal)assigned : null,
            submission.DraftGrade is { } draft ? (decimal)draft : null,
            LastTurnIn(submission.SubmissionHistory),
            submission.Late ?? false,
            submission.UpdateTimeDateTimeOffset);

    private static DateTimeOffset? LastTurnIn(IList<GoogleSubmissionHistory>? history) =>
        history?
            .Select(h => h.StateHistory)
            .Where(s => s?.State == TurnedInState && s.StateTimestampDateTimeOffset is not null)
            .Max(s => s!.StateTimestampDateTimeOffset);

    /// <summary>
    /// FR-008: the first of <c>scheduledTime</c>, the due date, <c>updateTime</c>, <c>creationTime</c>. When none is
    /// present the type's default instant is returned, which the entity refuses (db-design §3.3).
    /// </summary>
    private static DateTimeOffset ItemDate(
        DateTimeOffset? scheduled,
        DateTimeOffset? due,
        DateTimeOffset? updated,
        DateTimeOffset? created) =>
        scheduled ?? due ?? updated ?? created ?? default;

    /// <summary>
    /// db-design §3.4: Classroom's due date and due time are separate objects in UTC and come together. A date
    /// without a time is stored as no due date at all, never guessed at midnight or at end of day.
    /// </summary>
    private static DateTimeOffset? DueInstant(GoogleDate? date, GoogleTimeOfDay? time)
    {
        if (date?.Year is not { } year || date.Month is not { } month || date.Day is not { } day || time is null)
        {
            return null;
        }

        return new DateTimeOffset(year, month, day, time.Hours ?? 0, time.Minutes ?? 0, time.Seconds ?? 0, TimeSpan.Zero)
            .AddTicks((time.Nanos ?? 0) / 100);
    }

    /// <summary>
    /// One roster entry as Classroom gave it (OD-006): an entry whose profile carries no address is passed on with
    /// the address absent, never discarded and never given a placeholder — the prototype substituted one.
    /// </summary>
    private static RosterEntry Entry(string? userId, GoogleUserProfile? profile) =>
        new(userId ?? profile?.Id ?? string.Empty, profile?.EmailAddress, profile?.Name?.FullName);

    private ClassroomService CreateService(string impersonationUser) =>
        CreateService(impersonationUser, CourseAndRosterScopes);

    private ClassroomService CreateService(string impersonationUser, string[] scopes) =>
        new(new BaseClientService.Initializer
        {
            ApplicationName = ApplicationName,
            HttpClientFactory = _httpClients,
            HttpClientInitializer = Credential(impersonationUser, scopes),
            DefaultExponentialBackOffPolicy = ExponentialBackOffPolicy.None,
            GZipEnabled = false,
        });

    /// <summary>
    /// The delegated credential of one call: the school's technical account as the impersonated subject (BR-015,
    /// spec S-03), only the read-only scopes that call needs, and no retry of its own (OD-008).
    /// </summary>
    private ServiceAccountCredential Credential(string impersonationUser, string[] scopes)
    {
        var key = LoadKey()
            ?? throw new InvalidOperationException("The service-account key is not available to this installation.");

        return new ServiceAccountCredential(new ServiceAccountCredential.Initializer(key.Id, key.TokenServerUrl)
        {
            Key = key.Key,
            KeyId = key.KeyId,
            User = impersonationUser,
            Scopes = scopes,
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
