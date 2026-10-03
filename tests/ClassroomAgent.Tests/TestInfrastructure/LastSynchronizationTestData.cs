using System.Globalization;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// What the US-017 "Last synchronization" block and its tests must agree on: the translation keys (fixed by OD-010),
/// the diagnosis codes (spec FR-006), which diagnosis reuses the "Check access" text (spec FR-007, OD-006) and how an
/// instant is shown (spec FR-007, US-039 FR-009). One place, so the tests and the screen cannot drift.
/// </summary>
public static class LastSynchronizationTestData
{
    public const string Uk = "uk";

    public const string En = "en";

    /// <summary>The block's translation keys, fixed by US-017 OD-010; each exists in both resource files.</summary>
    public static class Keys
    {
        public const string Title = "LastSync.Title";

        public const string StatusNeverRun = "LastSync.Status.NeverRun";

        public const string StatusRunning = "LastSync.Status.Running";

        public const string StatusCompleted = "LastSync.Status.Completed";

        public const string StatusFailed = "LastSync.Status.Failed";

        public const string StartedAt = "LastSync.StartedAt";

        public const string FinishedAt = "LastSync.FinishedAt";

        public const string LastSuccess = "LastSync.LastSuccess";

        public const string LastSuccessNone = "LastSync.LastSuccess.None";

        public const string DiagnosisGoogleUnavailable = "LastSync.Diagnosis.GoogleUnavailable";

        public const string DiagnosisUnexpected = "LastSync.Diagnosis.Unexpected";

        public static readonly string[] All =
        [
            Title,
            StatusNeverRun,
            StatusRunning,
            StatusCompleted,
            StatusFailed,
            StartedAt,
            FinishedAt,
            LastSuccess,
            LastSuccessNone,
            DiagnosisGoogleUnavailable,
            DiagnosisUnexpected,
        ];
    }

    /// <summary>The six configuration diagnoses (spec FR-006); the block reuses the "Check access" text for each.</summary>
    public static readonly string[] ConfigurationCodes =
    [
        "ScopeNotAuthorized",
        "TechnicalAccountUnknown",
        "TechnicalAccountCannotRead",
        "ApiNotEnabled",
        "KeyUnavailable",
        "KeyRejected",
    ];

    /// <summary>The two codes the block words itself (spec FR-006, I-7).</summary>
    public static readonly string[] SynchronizationCodes = ["GoogleUnavailable", "Unexpected"];

    /// <summary>The translation key whose text the block shows for a diagnosis code.</summary>
    public static string DiagnosisKey(string code) => code switch
    {
        "GoogleUnavailable" => Keys.DiagnosisGoogleUnavailable,
        "Unexpected" => Keys.DiagnosisUnexpected,
        _ => "AccessCheck.Outcome." + code,
    };

    public static TheoryData<string> Languages => new(Uk, En);

    /// <summary>Every diagnosis code in each language.</summary>
    public static TheoryData<string, string> CodesByLanguage
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var language in new[] { Uk, En })
            {
                foreach (var code in ConfigurationCodes.Concat(SynchronizationCodes))
                {
                    data.Add(code, language);
                }
            }

            return data;
        }
    }

    /// <summary>A day of the month above 12, so the two cultures' short dates cannot coincide.</summary>
    public static readonly DateTimeOffset Finished = new(2026, 9, 23, 14, 5, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset Started = new(2026, 9, 23, 14, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset EarlierSuccess = new(2026, 9, 21, 9, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// An instant as the landing page shows the last legitimacy check: the culture's short date, <c>HH:mm</c> and
    /// <c>UTC</c> (<c>Views/Home/Index.cshtml</c>, US-039 FR-009).
    /// </summary>
    public static string Format(DateTimeOffset instant, string culture)
    {
        var info = CultureInfo.GetCultureInfo(culture);
        return instant.UtcDateTime.ToString(info.DateTimeFormat.ShortDatePattern + " HH:mm", info) + " UTC";
    }
}
