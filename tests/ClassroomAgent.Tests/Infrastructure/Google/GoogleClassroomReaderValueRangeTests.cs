using System.Globalization;
using System.Net;
using System.Text.Json;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Infrastructure.Google;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Infrastructure.Google;

/// <summary>
/// US-057 spec FR-001, VR-001, AC-001, AC-002, AC-010, §7 (SC-10, AD-4): the Google implementation of the Classroom port
/// turns a coursework maximum, an assigned grade or a draft grade beyond the <c>decimal</c> range into a course-scoped
/// <see cref="GoogleReadFailureKind.ValueOutOfRange"/> failure that carries neither the value nor an identifier, and
/// keeps today's conversion for every value within the range. Driven offline through a scripted transport with the
/// value sent as the JSON number Google would send (TC-4).
/// <para>
/// US-059 spec FR-008, VR-005, AC-009 (BR-043 v100): a value below zero is refused by the same conversion, as the same
/// failure; zero stays a value. A negative value just inside the <c>decimal</c> range, which US-057 kept, is now refused.
/// </para>
/// </summary>
public sealed class GoogleClassroomReaderValueRangeTests
{
    private const string Reference = "installation-google-key";

    private const string TechnicalAccount = AccessCheckTestData.TechnicalAccount;

    /// <summary>The three <c>double</c> → <c>decimal</c> conversions of the reader (VR-001).</summary>
    public static TheoryData<string> Fields => new("maxPoints", "assignedGrade", "draftGrade");

    /// <summary>
    /// Beyond the range in both directions (spec I-1): far beyond, and just beyond <c>decimal.MaxValue</c>
    /// (≈ 7.9228·10²⁸).
    /// </summary>
    public static TheoryData<string, double> OutOfRange
    {
        get
        {
            var data = new TheoryData<string, double>();
            foreach (var name in new[] { "maxPoints", "assignedGrade", "draftGrade" })
            {
                foreach (var value in new[] { 1e30, -1e30, 8e28, -8e28 })
                {
                    data.Add(name, value);
                }
            }

            return data;
        }
    }

    /// <summary>
    /// Just inside the range (US-057 AC-010). US-059 FR-008 refuses every negative value, so the lower bound −7.9·10²⁸
    /// moved to <see cref="Negative"/>.
    /// </summary>
    public static TheoryData<string, double> JustInside
    {
        get
        {
            var data = new TheoryData<string, double>();
            foreach (var name in new[] { "maxPoints", "assignedGrade", "draftGrade" })
            {
                data.Add(name, 7.9e28);
            }

            return data;
        }
    }

    /// <summary>US-059 VR-005: strictly below zero — the smallest step, an ordinary value and the old lower bound.</summary>
    public static TheoryData<string, double> Negative
    {
        get
        {
            var data = new TheoryData<string, double>();
            foreach (var name in new[] { "maxPoints", "assignedGrade", "draftGrade" })
            {
                foreach (var value in new[] { -1e-6, -0.25, -1, -7.9e28 })
                {
                    data.Add(name, value);
                }
            }

            return data;
        }
    }

    private static GoogleClassroomReader ReaderWith(ScriptedHttpHandler transport) =>
        new(
            new DictionarySecretStore(new Dictionary<string, string> { [Reference] = SyntheticServiceAccountKey.Create() }),
            new GoogleServiceAccountSettings(Reference),
            transport,
            new ManualTimeProvider(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero)),
            new FixedRetryJitter(1.0),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleClassroomReader>.Instance);

    private static HttpResponseMessage TokenIssued() =>
        ScriptedHttpHandler.JsonResponse(
            HttpStatusCode.OK,
            """{"access_token":"ya29.synthetic-access-token","expires_in":3599,"token_type":"Bearer"}""");

    private static HttpResponseMessage Page(string collection, params Dictionary<string, object?>[] entries) =>
        ScriptedHttpHandler.JsonResponse(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new Dictionary<string, object?> { [collection] = entries }));

    private static Dictionary<string, object?> Item(int ordinal, double? maxPoints) => new()
    {
        ["id"] = CourseWorkTestData.ItemId(ordinal),
        ["title"] = CourseWorkTestData.Title(ordinal),
        ["state"] = "PUBLISHED",
        ["creationTime"] = "2026-02-01T07:00:00Z",
        ["updateTime"] = "2026-02-02T07:00:00Z",
        ["maxPoints"] = maxPoints,
    };

    private static Dictionary<string, object?> Submission(int ordinal, double? assigned, double? draft) => new()
    {
        ["id"] = CourseWorkTestData.SubmissionId(ordinal),
        ["courseWorkId"] = CourseWorkTestData.ItemId(1),
        ["userId"] = CourseTestData.UserId(ordinal),
        ["state"] = "TURNED_IN",
        ["assignedGrade"] = assigned,
        ["draftGrade"] = draft,
        ["updateTime"] = "2026-02-02T07:00:00Z",
    };

    /// <summary>
    /// Reads the one field of one course: the coursework (an in-range item first, then the item carrying the value) or
    /// the submissions (an in-range one first, then the one carrying the value). Returns the converted value.
    /// </summary>
    private static async Task<decimal?> ReadAsync(string field, double value, CancellationToken ct)
    {
        if (field == "maxPoints")
        {
            var transport = ScriptedHttpHandler.Sequence(
                _ => TokenIssued(),
                _ => Page("courseWork", Item(1, 10), Item(2, value)),
                _ => Page("courseWorkMaterial"));
            var page = await ReaderWith(transport).ReadCourseWorkAsync(TechnicalAccount, CourseTestData.CourseId(1), ct);
            return page.Items.Single(i => i.GoogleId == CourseWorkTestData.ItemId(2)).Details.MaxPoints;
        }

        var submissions = ScriptedHttpHandler.Sequence(
            _ => TokenIssued(),
            _ => Page(
                "studentSubmissions",
                Submission(1, 5, 5),
                field == "assignedGrade" ? Submission(2, value, null) : Submission(2, null, value)));
        var read = await ReaderWith(submissions).ReadSubmissionsAsync(TechnicalAccount, CourseTestData.CourseId(1), ct);
        var carrying = read.Single(s => s.GoogleId == CourseWorkTestData.SubmissionId(2));
        return field == "assignedGrade" ? carrying.AssignedGrade : carrying.DraftGrade;
    }

    /// <summary>
    /// FR-001, VR-001, AC-001, AC-002: a value beyond the range is a <see cref="GoogleReadFailureKind.ValueOutOfRange"/>
    /// failure of the course's read — never an <see cref="OverflowException"/> leaving the adapter unclassified, which
    /// is what stops today's run as <c>Unexpected</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(OutOfRange))]
    public async Task AValueBeyondTheRange_IsAValueOutOfRangeFailure(string field, double value)
    {
        var ct = TestContext.Current.CancellationToken;

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(
            () => ReadAsync(field, value, ct).WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        Assert.Equal(GoogleReadFailureKind.ValueOutOfRange, failure.Kind);
    }

    /// <summary>
    /// FR-001, §7, SC-10: the failure — message, inner exceptions and all — carries neither the value nor the item,
    /// submission or user id, so nothing a log writes from it can disclose them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Fields))]
    public async Task TheFailure_CarriesNoValueAndNoIdentifier(string field)
    {
        var ct = TestContext.Current.CancellationToken;

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(
            () => ReadAsync(field, 1e30, ct).WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        var text = failure.ToString();
        foreach (var forbidden in new[]
                 {
                     "1E+30", "1e+30", "1E30", "1000000000000000000000000000000",
                     CourseWorkTestData.ItemId(2), CourseWorkTestData.Title(2),
                     CourseWorkTestData.SubmissionId(2), CourseTestData.UserId(2),
                 })
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// US-059 FR-008.1, FR-008.2, VR-005, AC-009: a value below zero fails the course's read as
    /// <see cref="GoogleReadFailureKind.ValueOutOfRange"/> — the out-of-range path — so no negative value reaches the
    /// Application and the database check constraint is never what stops the run.
    /// </summary>
    [Theory]
    [MemberData(nameof(Negative))]
    public async Task ANegativeValue_IsAValueOutOfRangeFailure(string field, double value)
    {
        var ct = TestContext.Current.CancellationToken;

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(
            () => ReadAsync(field, value, ct).WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        Assert.Equal(GoogleReadFailureKind.ValueOutOfRange, failure.Kind);
    }

    /// <summary>US-059 FR-008, SC-10: the failure for a negative value carries neither the value nor an identifier.</summary>
    [Theory]
    [MemberData(nameof(Fields))]
    public async Task TheNegativeValueFailure_CarriesNoValueAndNoIdentifier(string field)
    {
        var ct = TestContext.Current.CancellationToken;

        var failure = await Assert.ThrowsAsync<GoogleReadFailedException>(
            () => ReadAsync(field, -3.75, ct).WaitAsync(ManualTimeProvider.RealTimeLimit, ct));

        var text = failure.ToString();
        foreach (var forbidden in new[]
                 {
                     "3.75", "3,75",
                     CourseWorkTestData.ItemId(2), CourseWorkTestData.Title(2),
                     CourseWorkTestData.SubmissionId(2), CourseTestData.UserId(2),
                 })
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// US-059 FR-008.3: zero is a valid value and is converted as today. Green before the change by design — it pins
    /// that the check is strictly below zero.
    /// </summary>
    [Theory]
    [MemberData(nameof(Fields))]
    public async Task Zero_IsConvertedAsToday(string field)
    {
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal(0m, await ReadAsync(field, 0, ct));
    }

    /// <summary>
    /// FR-001, AC-010: a value just inside the range keeps today's conversion exactly — the check does not skip valid
    /// values. Green before the change by design: today's cast already converts these.
    /// </summary>
    [Theory]
    [MemberData(nameof(JustInside))]
    public async Task AValueJustInsideTheRange_IsConvertedAsToday(string field, double value)
    {
        var ct = TestContext.Current.CancellationToken;

        var converted = await ReadAsync(field, value, ct);

        Assert.Equal((decimal)value, converted);
        Assert.Equal(
            value.ToString("E3", CultureInfo.InvariantCulture),
            ((double)converted!.Value).ToString("E3", CultureInfo.InvariantCulture));
    }
}
