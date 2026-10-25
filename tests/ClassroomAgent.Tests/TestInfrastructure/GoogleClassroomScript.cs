using System.Net;
using System.Text.Json;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Infrastructure.Google;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-057: Google Classroom as JSON over a scripted transport, answered by route, so a host test runs the real
/// <see cref="GoogleClassroomReader"/> inside the real synchronization (TC-4: nothing reaches a network; every value is
/// synthetic). A value Google sends as a JSON number — points beyond the <c>decimal</c> range included — reaches the
/// adapter exactly as Google would send it, which a substituted port cannot do.
/// </summary>
public sealed class GoogleClassroomScript
{
    private const string KeyReference = "us057-google-key";

    private readonly List<Dictionary<string, object?>> _courses = [];
    private readonly Dictionary<string, List<Dictionary<string, object?>>> _students = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Dictionary<string, object?>>> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Dictionary<string, object?>>> _submissions = new(StringComparer.Ordinal);
    private readonly HashSet<(string CourseId, string Collection)> _forbidden = [];

    /// <summary>A course in the listing, in the order added.</summary>
    public GoogleClassroomScript Course(
        string id,
        string name,
        string state = "ACTIVE",
        string updateTime = "2026-02-02T07:00:00Z")
    {
        _courses.Add(new Dictionary<string, object?>
        {
            ["id"] = id,
            ["name"] = name,
            ["courseState"] = state,
            ["creationTime"] = "2026-02-01T07:00:00Z",
            ["updateTime"] = updateTime,
        });
        _students[id] = [];
        _items[id] = [];
        _submissions[id] = [];
        return this;
    }

    public GoogleClassroomScript Student(string courseId, string userId, string fullName, string email)
    {
        _students[courseId].Add(new Dictionary<string, object?>
        {
            ["userId"] = userId,
            ["profile"] = new Dictionary<string, object?>
            {
                ["id"] = userId,
                ["name"] = new Dictionary<string, object?> { ["fullName"] = fullName },
                ["emailAddress"] = email,
            },
        });
        return this;
    }

    /// <summary>A published coursework item; <paramref name="maxPoints"/> is the JSON number Google sends.</summary>
    public GoogleClassroomScript Item(string courseId, string itemId, string title, double? maxPoints)
    {
        _items[courseId].Add(new Dictionary<string, object?>
        {
            ["id"] = itemId,
            ["title"] = title,
            ["state"] = "PUBLISHED",
            ["creationTime"] = "2026-02-01T07:00:00Z",
            ["updateTime"] = "2026-02-02T07:00:00Z",
            ["maxPoints"] = maxPoints,
        });
        return this;
    }

    /// <summary>A turned-in submission; the grades are the JSON numbers Google sends.</summary>
    public GoogleClassroomScript Submission(
        string courseId,
        string itemId,
        string submissionId,
        string userId,
        double? assignedGrade = null,
        double? draftGrade = null)
    {
        _submissions[courseId].Add(new Dictionary<string, object?>
        {
            ["id"] = submissionId,
            ["courseWorkId"] = itemId,
            ["userId"] = userId,
            ["state"] = "TURNED_IN",
            ["assignedGrade"] = assignedGrade,
            ["draftGrade"] = draftGrade,
            ["late"] = false,
            ["updateTime"] = "2026-02-02T07:00:00Z",
        });
        return this;
    }

    /// <summary>
    /// The course's teacher list answers <c>403</c> — the technical account's rights, a configuration failure that
    /// stops the run with <c>TechnicalAccountCannotRead</c> after one attempt (US-017 FR-001).
    /// </summary>
    public GoogleClassroomScript ForbidRoster(string courseId)
    {
        _forbidden.Add((courseId, "teachers"));
        return this;
    }

    /// <summary>The transport every request of the reader goes through; recorded for inspection.</summary>
    public ScriptedHttpHandler Transport { get; private set; } = null!;

    /// <summary>Registers the real reader over this script in the host; call before the host starts.</summary>
    public void InstallIn(InstallationTestHost host)
    {
        Transport = new ScriptedHttpHandler((request, _) => Task.FromResult(Answer(request)));
        var previous = host.ConfigureServices;
        host.ConfigureServices = services =>
        {
            previous?.Invoke(services);
            services.RemoveAll<IClassroomReader>();
            services.AddSingleton<IClassroomReader>(provider => new GoogleClassroomReader(
                new DictionarySecretStore(new Dictionary<string, string> { [KeyReference] = SyntheticServiceAccountKey.Create() }),
                new GoogleServiceAccountSettings(KeyReference),
                Transport,
                provider.GetRequiredService<TimeProvider>(),
                new FixedRetryJitter(1.0),
                provider.GetRequiredService<ILogger<GoogleClassroomReader>>()));
        };
    }

    private HttpResponseMessage Answer(HttpRequestMessage request)
    {
        var uri = request.RequestUri!;
        if (uri.Host == "oauth2.googleapis.com")
        {
            return Json(new Dictionary<string, object?>
            {
                ["access_token"] = "ya29.synthetic-access-token",
                ["expires_in"] = 3599,
                ["token_type"] = "Bearer",
            });
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/');

        // v1/courses
        if (segments is ["v1", "courses"])
        {
            return Json(new Dictionary<string, object?> { ["courses"] = _courses });
        }

        // v1/courses/{id}/courseWork/-/studentSubmissions
        if (segments is ["v1", "courses", var subCourse, "courseWork", "-", "studentSubmissions"])
        {
            return Json(new Dictionary<string, object?> { ["studentSubmissions"] = _submissions[subCourse] });
        }

        if (segments is ["v1", "courses", var courseId, var collection])
        {
            if (_forbidden.Contains((courseId, collection)))
            {
                return Forbidden();
            }

            return collection switch
            {
                "teachers" => Json(new Dictionary<string, object?> { ["teachers"] = Array.Empty<object>() }),
                "students" => Json(new Dictionary<string, object?> { ["students"] = _students[courseId] }),
                "courseWork" => Json(new Dictionary<string, object?> { ["courseWork"] = _items[courseId] }),
                "courseWorkMaterials" => Json(new Dictionary<string, object?> { ["courseWorkMaterial"] = Array.Empty<object>() }),
                _ => throw new InvalidOperationException($"Unscripted Classroom collection '{collection}'."),
            };
        }

        throw new InvalidOperationException($"Unscripted Google request '{uri.AbsolutePath}'.");
    }

    private static HttpResponseMessage Json(object body) =>
        ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, JsonSerializer.Serialize(body));

    private static HttpResponseMessage Forbidden() =>
        ScriptedHttpHandler.JsonResponse(
            HttpStatusCode.Forbidden,
            JsonSerializer.Serialize(new
            {
                error = new
                {
                    code = 403,
                    message = "Not Authorized to access this resource/api",
                    status = "PERMISSION_DENIED",
                    errors = new[] { new { message = "Not Authorized to access this resource/api", domain = "global", reason = "forbidden" } },
                    details = new[]
                    {
                        new Dictionary<string, string>
                        {
                            ["@type"] = "type.googleapis.com/google.rpc.ErrorInfo",
                            ["reason"] = "forbidden",
                            ["domain"] = "googleapis.com",
                        },
                    },
                },
            }));
}
